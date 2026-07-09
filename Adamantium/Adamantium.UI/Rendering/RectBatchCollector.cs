using System;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Item-background batch (the "подложки" instancing): collects same-clip SOLID rounded-rect fills - each baked to WORLD
// space on the CPU - into ONE instanced draw per segment (RectBatchEffect reconstructs the rounded corners from an SDF
// = self-anti-aliasing, so N item backgrounds cost ~1 draw AND no separate AA fringe). Segment/buffer/overlap
// machinery is in BatchCollector; this adds rect baking + the SDF draw. Rendered BELOW the text batch (lower layer).
internal sealed class RectBatchCollector : SdfBatchCollector<RectItem>
{
    // A/B / safety-valve toggle: off routes every rect back to its per-unit fill + AA-fringe draw (the pre-batch path).
    public static bool Enabled = true;

    public RectBatchCollector() : base(4096) { }

    protected override IEffectPass DrawPass => Effect.BatchRectPass;

    // Batchable = a visible solid fill + a batchable pen (none, or a SOLID stroke the SDF shader draws analytically),
    // uniform corner radius. Gradient/image fill, a non-solid/dashed/trimmed pen, per-corner radii, or Enabled=off fall
    // back to the per-unit draw. Must stay in lock-step with EllipseRenderUnit/RectangleRenderUnit.IsSdfBatchable.
    public bool CanBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not (null or SolidColorBrush)) return false;   // gradient/image FILL -> fallback
        if (!IsPenBatchable(p.Pen)) return false;
        // Need at least a visible fill OR a visible stroke (a hollow stroked rect batches too - fill just alpha 0).
        var hasFill = p.Brush is SolidColorBrush { Color.A: > 0 };
        var hasStroke = p.Pen is { Brush: SolidColorBrush { Color.A: > 0 } };
        if (!hasFill && !hasStroke) return false;
        var c = p.CornerRadius;
        return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight && c.BottomRight == c.BottomLeft;
    }

    // A pen the SDF stroke shader can draw analytically: none, or a SOLID-colour stroke. Dashes are supported as a single
    // ON/GAP period (a 2-element array); a longer custom pattern still falls back to the compute expander. Trim, dash
    // offset and thickness are all handled per-fragment (see BatchEffect.fx), so a dashed/trimmed stroke still BATCHES -
    // which is what lets the whole virtualized grid dash without per-tile GPU buffers (the device-memory OOM).
    internal static bool IsPenBatchable(Pen pen)
    {
        if (pen == null) return true;
        if (pen.Brush is not SolidColorBrush) return false;
        var dash = pen.DashStrokeArray;
        return dash is not { Count: > 0 } || dash.Count == 2;
    }

    // Bake a pen into the instance's stroke fields (shared by the rect + ellipse batches). Colour with opacity folded;
    // width/dash/offset scale by the world device scale (sx) into device px, matching the arc-length `s` the shader
    // computes; trim is a 0..1 fraction. CENTRE-aligned (half in / half out). No pen -> a zero stroke (fill only).
    internal static void BakeStroke(Pen pen, double opacity, float sx,
        out Vector4F strokeColor, out Vector4F stroke0, out Vector4F stroke1)
    {
        strokeColor = Vector4F.Zero;
        stroke0 = Vector4F.Zero;
        stroke1 = new Vector4F(0, 0, 1, 0);   // dashOffset=0, trimStart=0, trimEnd=1, flags=0
        if (pen?.Brush is not SolidColorBrush penBrush) return;

        var sc = penBrush.Color.ToVector4();
        sc.W *= (float)(opacity * penBrush.Opacity);
        strokeColor = sc;

        float dashOn = 0f, dashGap = 0f;
        if (pen.DashStrokeArray is { Count: 2 } d)
        {
            dashOn = (float)(d[0] * sx);
            dashGap = (float)(d[1] * sx);
        }
        // Packed for the shader: caps base-8 (codes below) - DashCap for dash-piece ends, Start/EndLineCap for the
        // contour's real ends (only exist when trimmed); the JOIN (0 miter, 1 bevel, 2 round) in the 512s place.
        var capFlags = CapCode(pen.DashCap) + 8f * CapCode(pen.StartLineCap) + 64f * CapCode(pen.EndLineCap)
                     + 512f * JoinCode(pen.PenLineJoin);
        stroke0 = new Vector4F((float)(pen.Thickness * sx), 0f, dashOn, dashGap);
        stroke1 = new Vector4F((float)(pen.DashOffset * sx), (float)pen.TrimStart, (float)pen.TrimEnd, capFlags);
    }

    // All six caps, drawn analytically by CapReach in BatchEffect.fx. Codes MATCH the geometry stroker's MapCap so the two
    // stroke paths render the same shape: 0 flat, 1 square, 2 convex round, 3 convex triangle, 4 concave triangle, 5 concave round.
    private static float CapCode(PenLineCap cap) => cap switch
    {
        PenLineCap.Square => 1f,
        PenLineCap.ConvexRound => 2f,
        PenLineCap.ConvexTriangle => 3f,
        PenLineCap.ConcaveTriangle => 4f,
        PenLineCap.ConcaveRound => 5f,
        _ => 0f   // Flat
    };

    // The three outer-corner joins the SDF draws (see SdRoundRectJoin): 0 miter (sharp), 1 bevel (chamfer), 2 round.
    private static float JoinCode(PenLineJoin join) => join switch
    {
        PenLineJoin.Bevel => 1f,
        PenLineJoin.Round => 2f,
        _ => 0f
    };

    // Bake one solid rounded-rect fill (position -> world, colour straight with opacity folded in) into the pending
    // segment. False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow this frame) - the
    // caller then draws that rect via the per-unit path.
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds)
    {
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;

        var color = Vector4F.Zero;
        if (p.Brush is SolidColorBrush solid)
        {
            color = solid.Color.ToVector4();
            color.W *= (float)(opacity * solid.Opacity);
        }

        // Stroke (optional): the full pen baked to the instance (colour + device-px width, dash on/gap, dash offset,
        // trim), CENTRE-aligned (half in / half out). Solid, dashed and trimmed strokes all draw analytically in the SDF
        // shader, so a stroked tile stays in the batch (no per-tile GPU buffers) - the whole grid can dash without OOM.
        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1);

        var r = p.DestinationRect;
        Items[Count++] = new RectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F((float)(p.CornerRadius.TopLeft * sx), 0, 0, 0),
            Color = color,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1
        };
        MarkPending(scissor, logicalBounds);
        return true;
    }
}
