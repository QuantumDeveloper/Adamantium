using System;
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
internal sealed class EllipseBatchCollector : SdfBatchCollector<EllipseItem>
{
    // A/B / safety-valve toggle: off routes every ellipse back to its per-unit tessellated fill + AA-fringe draw.
    public static bool Enabled = true;

    public EllipseBatchCollector() : base(2048) { }

    protected override IEffectPass DrawPass => Effect.BatchEllipsePass;

    // Batchable = a visible solid fill + a batchable pen (none, or a SOLID stroke the SDF shader draws analytically), a
    // FULL ellipse (StartAngle 0 .. SweepAngle 360). A sector/arc, a non-solid/dashed/trimmed pen, a gradient/image fill,
    // or Enabled=off falls back to the per-unit tessellated draw. Lock-step with EllipseRenderUnit.IsSdfBatchable.
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
        // An ARC is just an angular slice the pixel shader cuts out of the same field, so it batches like anything else.
        // A SECTOR does not: it closes the wedge with two straight radial edges, which is a different outline and not
        // something the ellipse field describes - that one keeps the tessellated path.
        // TODO(arc): the shader can cut the wedge (EllipseData.Arc), but batching arcs currently loses the device -
        // unfinished, so arcs still take the tessellated path. Re-enable together with the EdgeToEdge check.
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    public bool CanBatch(EllipsePayload p) => WantsBatch(p);

    // Bake one solid ellipse fill (bounds -> world, colour straight with opacity folded in) into the pending segment.
    // False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow this frame) - the caller then draws
    // that ellipse via the per-unit path.
    public bool TryAdd(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, out var item)) return false;   // rotation/shear -> per-unit
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake one solid ellipse into an instance record, WITHOUT appending it. Shared by TryAdd (append) and the paint
    // fast-path (re-bake an existing slot in place - see RenderCache.TryPartialReplay), exactly as the rect batch does.
    // False = not bakeable this way (rotated/sheared world); the caller draws it per-unit.
    public static bool BakeItem(EllipsePayload p, Matrix4x4F world, double opacity, int transformSlot, out EllipseItem item)
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
        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1,
            EllipsePerimeter(r.Width / 2.0, r.Height / 2.0));
        item = new EllipseItem
        {
            // NODE-local when transformSlot != 0 (world is then the transform RELATIVE to the motion node; the vertex
            // shader applies the node's table matrix on top - the O(1)-scroll path). Slot 0 = identity = world bake.
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F(transformSlot, 0, 0, 0),
            Color = color,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1
        };
        return true;
    }

    // Ramanujan's second approximation - exact enough that the dash fit lands on a whole number of periods (the error is
    // parts per billion for any ratio a UI produces), and there is no closed form to be exact with.
    private static double EllipsePerimeter(double a, double b)
    {
        if (a <= 0 || b <= 0) return 0;

        var h = (a - b) * (a - b) / ((a + b) * (a + b));
        return Math.PI * (a + b) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
    }
}
