using System;
using Adamantium.UI.Core.Graphics;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Ellipse/circle SDF batch (the "SDF family", docs/PER_MONITOR_DPI_PLAN.md): collects same-clip SOLID full-ellipse fills
// - each baked to WORLD space on the CPU - into ONE instanced draw. The shader evaluates the ellipse implicit and
// self-anti-aliases via fwidth, so N circles/ellipses cost ~1 draw AND are resolution-independent (no tessellation, crisp
// at any DPI/zoom, no AA fringe). Sibling of RectBatchCollector; drawn in the SAME fill layer (below the text batch).
internal sealed class EllipseBatchCollector : ShapeSdfCollector<EllipseItem>
{
    // A/B / safety-valve toggle: off routes every ellipse back to its per-unit tessellated fill + AA-fringe draw.
    public static bool Enabled = true;

    public EllipseBatchCollector() : base(2048) { }

    protected override IEffectPass DrawPass => Effect.BatchEllipsePass;

    // Batchable = a visible solid fill + a batchable pen (none, or a SOLID stroke the SDF shader draws analytically). A
    // SECTOR and a SEGMENT batch too: both are this same ellipse with a straight boundary added, which the field
    // intersects (see EllipseCutDistance) - so neither needs a shape, a pass or a collector of its own. A gradient/image
    // fill, a non-solid pen or Enabled=off still falls back to the per-unit tessellated draw. Lock-step with
    // EllipseRenderUnit.IsSdfBatchable.
    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(EllipsePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not (null or SolidColorBrush)) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        // Something to draw = a fill OR a stroke, the same rule the rect batch uses. Demanding a NON-TRANSPARENT fill was
        // left over from when this batch could only fill; it strokes analytically now, so the demand only threw every
        // RING (transparent fill + stroke - a spinner, a progress ring, an outlined dot) out to per-unit tessellation.
        var hasFill = p.Brush is SolidColorBrush { Color.A: > 0 };
        var hasStroke = p.Pen is { Brush: SolidColorBrush { Color.A: > 0 } };
        if (!hasFill && !hasStroke) return false;
        return IsCutBatchable(p);
    }

    /// <summary>Whether the angular cut is one this batch can draw. A WHOLE ellipse always is. A partial one is, with two
    /// honest exceptions:
    /// <list type="bullet">
    /// <item>a NEGATIVE sweep - the tessellator mirrors the whole traversal for it (start included), and a batch that
    /// guessed at that rule would draw a different shape than the fallback;</item>
    /// <item>a DASHED or TRIMMED pen on a cut shape - dashes are placed by arc length along the ELLIPSE, and a sector's
    /// outline is arc plus straight edges, so the pattern would be fitted to a contour the shape does not have.</item>
    /// </list></summary>
    internal static bool IsCutBatchable(EllipsePayload p)
    {
        if (p.SweepAngle >= 360.0) return true;   // not cut at all - see BakeCut, the start angle is irrelevant here
        if (p.SweepAngle <= 0.0) return false;

        var pen = p.Pen;
        if (pen == null) return true;
        var dashed = pen.DashStrokeArray is { Count: > 0 };
        return !dashed && pen.TrimStart <= 0.0 && pen.TrimEnd >= 1.0;
    }

    public bool CanBatch(EllipsePayload p) => WantsBatch(p);

    // Bake one solid ellipse fill (bounds -> world, colour straight with opacity folded in) into the pending segment.
    // False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow this frame) - the caller then draws
    // that ellipse via the per-unit path.
    public bool TryAdd(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0,
        int fadeSlot = -1, int clipSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, fadeSlot, out var item)) return false;   // rotation/shear -> per-unit
        item.Params.Z = clipSlot;
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake one solid ellipse into an instance record, WITHOUT appending it. Shared by TryAdd (append) and the paint
    // fast-path (re-bake an existing slot in place - see RenderCache.TryPartialReplay), exactly as the rect batch does.
    // False = not bakeable this way (rotated/sheared world); the caller draws it per-unit.
    public static bool BakeItem(EllipsePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot, out EllipseItem item)
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

        // Stroke (optional): the full pen baked to the instance (colour + device-px width, dash on/gap, offset, trim),
        // CENTRE-aligned. Solid/dashed/trimmed all draw analytically in the SDF shader, so a stroked ellipse stays in the
        // batch (no per-tile GPU buffers).
        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var r = p.DestinationRect;
        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1, out var dash,
            EllipsePerimeter(r.Width / 2.0, r.Height / 2.0));
        item = new EllipseItem
        {
            // NODE-local when transformSlot != 0 (world is then the transform RELATIVE to the motion node; the vertex
            // shader applies the node's table matrix on top - the O(1)-scroll path). Slot 0 = identity = world bake.
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            // .z = the rounded CLIP slot, and it starts at -1, never 0: zero is a perfectly valid slot belonging to
            // somebody else, so a record that forgot to set it would be cut by a stranger's shape.
            Params = new Vector4F(transformSlot, fadeSlot, -1, 0),
            Color = color,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1,
            Dash = dash,
            Arc = BakeCut(p, sx)
        };
        return true;
    }

    // The angular cut, in the ellipse's own PARAMETRIC angle (radians) - the angle the tessellator sweeps, so the batch
    // and the fallback cut at the same place. .z says how the shape closes: 0 whole, 1 sector (through the centre),
    // 2 edge-to-edge (by the chord).
    private static Vector4F BakeCut(EllipsePayload p, double sx)
    {
        // .w is the RING, and it is independent of the cut: a whole ellipse can be a ring, and a sector can have a hole.
        var ring = (float)(p.RingThickness * sx);
        // A WHOLE sweep is not cut at all, and the start angle has nothing to say about it - where a closed contour begins
        // is not a property of the shape. Baking a cut anyway put both bounding rays of the wedge on the SAME ray, and
        // anti-aliasing that non-existent edge left a one-pixel seam running out from the centre. The tessellator draws
        // this case closed (its `isClosed` asks only about the sweep), so cutting here also split the two paths.
        if (p.SweepAngle >= 360.0) return new Vector4F(0, 0, 0, ring);

        var start = MathHelper.DegreesToRadians(p.StartAngle);
        var end = start + MathHelper.DegreesToRadians(p.SweepAngle);
        // A BAND's ends are RADIAL, whichever closing was asked for. A chord across a band is not a shape anybody means by
        // a ring gauge or a donut slice, and it is also the closing the tessellated fallback cannot reproduce: there the
        // ring is the outer shape minus the inner one, and only the radial ends of the two line up exactly.
        var kind = p.EllipseType == EllipseType.Sector || ring > 0 ? 1f : 2f;
        return new Vector4F((float)start, (float)end, kind, ring);
    }

    // Ramanujan's second approximation - exact enough that the dash fit lands on a whole number of periods (the error is
    // parts per billion for any ratio a UI produces), and there is no closed form to be exact with.
    private static double EllipsePerimeter(double a, double b)
    {
        if (a <= 0 || b <= 0) return 0;

        var h = (a - b) * (a - b) / ((a + b) * (a + b));
        return Math.PI * (a + b) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
    }

    /// <summary>Bake one unit into the patch stage - see BatchArena. Same bake TryAdd uses; it just lands in the stage
    /// instead of the arena, because a patch has to know the whole frame is repairable before it changes any of it.</summary>
    public override bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot, int ownerTag, int clipSlot = -1)
    {
        if (unit is not RenderUnits.EllipseRenderUnit u || !CanBatch(u.EllipsePayload)) return false;
        if (!BakeItem(u.EllipsePayload, world, u.FillOpacity, transformSlot, unit.FadeSlot, out var item)) return false;

        item.Params.Z = clipSlot;   // the same stamp TryAdd makes - see BatchArena.TryStage
        Stage.Add(item);
        return true;
    }
}
