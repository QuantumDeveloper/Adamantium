using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Gradient rounded-rect batch: draws MANY rounded-rect fills whose fill is a LINEAR or RADIAL gradient in ONE instanced
// draw (each fill = one per-instance GradientRectItem; the pixel shader reconstructs the rounded rect from an SDF AND
// evaluates the gradient per fragment). Sibling of the solid RectBatchCollector - a gradient fill routes here, a solid
// fill stays in the cheaper RectBatch. Segment/buffer/overlap/retain machinery comes from SdfBatchCollector; this adds
// the gradient bake + the GradientRect draw pass. Up to GradientRectItem.MaxStops colour stops. The bake is shared with
// the gradient ELLIPSE batch (GradientEllipseCollector) via BakeGradientItem - the two differ only in the pixel SDF.
internal sealed class GradientRectCollector : SdfBatchCollector<GradientRectItem>
{
    public static bool Enabled = true;

    public GradientRectCollector() : base(1024) { }

    protected override IEffectPass DrawPass => Effect.BatchGradientPass;

    // Batchable = a gradient (linear/radial) fill, a batchable pen (none or a solid stroke the SDF shader draws), and
    // uniform corner radius. Mirrors RectangleRenderUnit.IsGradientBatchable.
    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not GradientBrush g) return false;
        if (g is not MeshGradientBrush && g.GradientStops.Count == 0) return false;   // mesh carries corners, not stops
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return true;
    }

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    // Bake one gradient rounded-rect fill. False only if it can't be baked (rotated/sheared world or a GPU-buffer
    // overflow this frame) - the caller draws it per-unit.
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, out var item)) return false;
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake one gradient rounded-rect WITHOUT appending it - the paint fast-path re-bakes an existing slot in place (a
    // sweeping shimmer moves its stops every tick and nothing else about the element changes). See RectBatchCollector.BakeItem.
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, out GradientRectItem item)
    {
        item = default;
        if (p.Brush is not GradientBrush g) return false;
        return BakeGradientItem(g, p.DestinationRect, p.CornerRadius, p.Pen, world, opacity, 0f, transformSlot, out item);
    }

    // Bake a gradient fill into an instance record (shared by the rect + ellipse gradient batches). Position -> world;
    // gradient geometry + stops are RELATIVE to the rect (0..1), so one brush paints any size. False on a rotated/sheared
    // world (the axis-aligned instance can't hold it). shape (Geom1.z) selects the shader SDF: 0 rounded-rect, 1 ellipse.
    internal static bool BakeGradientItem(GradientBrush g, Rect dest, ProceduralGeometry.CornerRadius corners, Pen pen, Matrix4x4F world,
        double opacity, float shape, int transformSlot, out GradientRectItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var alpha = (float)(opacity * g.Opacity);
        item.Bounds = new Vector4F((float)(dest.X * sx + tx), (float)(dest.Y * sy + ty), (float)(dest.Width * sx), (float)(dest.Height * sy));

        Span<Vector4F> cols = stackalloc Vector4F[GradientBake.MaxStops];
        Span<float> offs = stackalloc float[GradientBake.MaxStops];
        var count = GradientBake.PackStops(g, alpha, cols, offs);
        item.Stop0 = cols[0]; item.Stop1 = cols[1]; item.Stop2 = cols[2]; item.Stop3 = cols[3];
        item.Stop4 = cols[4]; item.Stop5 = cols[5]; item.Stop6 = cols[6]; item.Stop7 = cols[7];
        item.Offsets0 = new Vector4F(offs[0], offs[1], offs[2], offs[3]);
        item.Offsets1 = new Vector4F(offs[4], offs[5], offs[6], offs[7]);

        var type = GradientBake.PackGeometry(g, out var geom0, out var geom1);
        item.Geom0 = geom0;
        item.Geom1 = geom1;
        item.Geom1.Z = shape;          // shader shape branch: 0 rounded-rect, 1 ellipse (Geom1.z is otherwise spare)
        item.Geom1.W = transformSlot;  // transform-table slot (0 = identity world bake; node-local otherwise)

        RectBatchCollector.BakeStroke(pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1, out var dash);
        item.StrokeColor = strokeColor;
        item.Stroke0 = stroke0;
        item.Stroke1 = stroke1;
        item.Dash = dash;

        // Params.w packs BOTH the spread (0 pad/1 reflect/2 repeat) and the colour-interpolation mode (0 sRGB/1 OKLab):
        // spread + 8*mode. The shader unpacks (packed & 7) for spread and (packed >> 3) for the mode - no extra record field.
        item.Radii = RectBatchCollector.BakeRadii(corners, dest, sx);
        item.Params = new Vector4F(RectBatchCollector.MaxOf(item.Radii), type, count, (float)g.SpreadMethod + 8f * (float)g.ColorInterpolationMode);
        return true;
    }
}
