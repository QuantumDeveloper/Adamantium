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
    public bool CanBatch(EllipsePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not SolidColorBrush s || s.Color.A == 0) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

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
        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1);

        var r = p.DestinationRect;
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
}
