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
    /// <summary>THE one statement of what this batch draws. Static because the render UNIT has to ask the same question
    /// before it builds anything - a unit that answered it on its own copy of the rules is how they drift apart, and a
    /// drifted answer means either wasted per-unit machinery or a shape drawn twice.</summary>
    public static bool WantsBatch(RectanglePayload p)
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

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    // A pen the SDF stroke shader can draw analytically: none, or a SOLID-colour stroke. Dashes are supported as a single
    // ON/GAP period (a 2-element array); a longer custom pattern still falls back to the compute expander. Trim, dash
    // offset and thickness are all handled per-fragment (see BatchEffect.fx), so a dashed/trimmed stroke still BATCHES -
    // which is what lets the whole virtualized grid dash without per-tile GPU buffers (the device-memory OOM).
    internal static bool IsPenBatchable(Pen pen)
    {
        if (pen == null) return true;
        if (pen.Brush is not SolidColorBrush) return false;
        var dash = pen.DashStrokeArray;
        if (dash is not { Count: > 0 }) return true;
        if (dash.Count != 2) return false;

        // The analytic mask asks "is the arc length of the NEAREST contour point inside a dash". `d` is continuous, but
        // that arc length is NOT: at a corner the nearest point jumps from one edge to the other across the bisector,
        // and the arc jumps with it by up to a whole thickness. A mask is a function of that arc, so it inherits the
        // jump - as a dash boundary crossed for no reason, which is a phantom dash END, which draws a CAP across the
        // ribbon in the middle of a corner. Every artifact we chased there (seam, hole, phantom scrap, transverse
        // bite) is that one discontinuity. It is not a tuning problem: the honest question is a DISTANCE to the dashed
        // path, which no single sample of the arc can answer.
        // So the batch declines what it cannot represent, and the compute expander (which builds the dash pieces as
        // real geometry, with cap frames and joins) takes it.
        // The bound is deliberately NOT "the corner is as round as the stroke is thick", even though that is where the
        // model actually stops being exact: two different renderers cannot agree pixel for pixel, so wherever the
        // switch sits it SHOWS - and a threshold in the middle of the useful range flips the whole picture on a hair of
        // thickness (7.7 vs 8.0 at corner 4 was exactly that). It sits instead where the difference is smaller than a
        // pixel: a stroke thin enough that no cap or corner detail resolves. That is also the case batching exists for
        // - a whole virtualized grid of one-pixel dashed borders, drawn without a GPU buffer per tile.
        return pen.Thickness * 0.5 <= 1.5;
    }

    // Bake a pen into the instance's stroke fields (shared by the rect + ellipse batches). Colour with opacity folded;
    // width/dash/offset scale by the world device scale (sx) into device px, matching the arc-length `s` the shader
    // computes; trim is a 0..1 fraction. CENTRE-aligned (half in / half out). No pen -> a zero stroke (fill only).
    /// <param name="contourLength">Length of the outline being stroked, in LOCAL units, or 0 when the caller cannot say.
    /// Only <see cref="Pen.FitDashesToContour"/> needs it: the pattern is stretched so a whole number of periods goes
    /// round, which is what keeps a closed dashed ring from carrying one long dash at its seam.</param>
    internal static void BakeStroke(Pen pen, double opacity, float sx,
        out Vector4F strokeColor, out Vector4F stroke0, out Vector4F stroke1, double contourLength = 0)
    {
        strokeColor = Vector4F.Zero;
        stroke0 = Vector4F.Zero;
        stroke1 = new Vector4F(0, 0, 1, 0);   // dashOffset=0, trimStart=0, trimEnd=1, flags=0
        if (pen?.Brush is not SolidColorBrush penBrush) return;

        var sc = penBrush.Color.ToVector4();
        sc.W *= (float)(opacity * penBrush.Opacity);
        strokeColor = sc;

        float dashOn = 0f, dashGap = 0f;
        var period = 0.0;
        var fit = 1.0;
        if (pen.DashStrokeArray is { Count: 2 } d)
        {
            period = d[0] + d[1];
            // Fit the pattern to the outline: without it the leftover of the last period lands where the contour closes
            // and reads as one long dash, and it moves about as the shape resizes.
            if (pen.FitDashesToContour && period > 0 && contourLength > 0)
            {
                var periods = Math.Max(1.0, Math.Round(contourLength / period));
                fit = contourLength / (periods * period);
            }

            dashOn = (float)(d[0] * fit * sx);
            dashGap = (float)(d[1] * fit * sx);
        }
        // Packed for the shader: four caps base-8 (codes below) - the two DASH caps (a dash's own two ends, separate so
        // it can be asymmetric), then Start/EndLineCap for the contour's real ends (which only exist when trimmed);
        // the JOIN (0 miter, 1 bevel, 2 round) sits above them all.
        var capFlags = CapCode(pen.DashStartCap) + 8f * CapCode(pen.DashEndCap)
                     + 64f * CapCode(pen.StartLineCap) + 512f * CapCode(pen.EndLineCap)
                     + 4096f * JoinCode(pen.PenLineJoin);
        stroke0 = new Vector4F((float)(pen.Thickness * sx), 0f, dashOn, dashGap);
        // DashPhase is in PERIODS - so an animation runs 0 -> 1 and lands back on itself whatever the array says - and
        // becomes pixels here, alongside the pixel offset. Both take the fit, or a ring seamless in shape would still
        // drift in phase as it marched.
        var offset = (pen.DashOffset + pen.DashPhase * period) * fit;
        stroke1 = new Vector4F((float)(offset * sx), (float)pen.TrimStart, (float)pen.TrimEnd, capFlags);
    }

    // Outline length of a rounded rect: the four straight runs plus the corner arcs (four quarter-circles = one full
    // circle when the radii are uniform). Only the dash FIT reads it, and only uniform corners batch here anyway.
    private static double RoundedRectPerimeter(Rect rect, ProceduralGeometry.CornerRadius corners)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return 0;

        var r = Math.Max(0, corners.TopLeft);
        r = Math.Min(r, Math.Min(rect.Width, rect.Height) / 2.0);
        return 2 * (rect.Width - 2 * r) + 2 * (rect.Height - 2 * r) + 2 * Math.PI * r;
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

    // Bake one solid rounded-rect fill (position -> world, colour straight with opacity folded in) into an instance.
    // False = not bakeable this way (rotated/sheared world). Shared by TryAdd (append) AND the partial-replay UpdateSlot
    // path, which re-bakes ONE dirty tile in place (a hover recolour) without re-walking the scene.
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, out RectItem item)
        => BakeItem(p, world, opacity, 0, out item);

    /// <summary><paramref name="transformSlot"/> = the instance's transform-table slot (0 = identity for a world-space
    /// bake; a motion node's slot for a NODE-LOCAL bake - <paramref name="world"/> is then the transform RELATIVE to
    /// that node and the vertex shader applies the node's matrix on top - the O(1)-scroll path).</summary>
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, out RectItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

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
        BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1,
            RoundedRectPerimeter(p.DestinationRect, p.CornerRadius));

        var r = p.DestinationRect;
        item = new RectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            // .z = 1 means "no fringe": the shader takes the edge hard instead of fading it over a pixel.
            Params = new Vector4F((float)(p.CornerRadius.TopLeft * sx), transformSlot, p.AntiAlias ? 0 : 1, 0),
            Color = color,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1
        };
        return true;
    }

    // Bake one solid rounded-rect fill into the pending segment. False only if it can't be baked (rotated/sheared world
    // or a GPU-buffer overflow this frame) - the caller then draws that rect via the per-unit path.
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, out var item)) return false;   // rotation/shear -> per-unit
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }
}
