using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Gradient ellipse batch: draws MANY full-ellipse fills with a LINEAR/RADIAL gradient in ONE instanced draw. Sibling of
// the solid EllipseBatchCollector - a gradient ellipse fill routes here. Reuses the gradient RECT instance record + bake
// (GradientRectItem / GradientRectCollector.BakeGradientItem) - only the pixel-shader SDF differs (pass GradientEllipse).
internal sealed class GradientEllipseCollector : SdfBatchCollector<GradientRectItem>
{
    public static bool Enabled = true;

    public GradientEllipseCollector() : base(1024) { }

    protected override IEffectPass DrawPass => Effect.BatchGradientPass;

    // Batchable = a gradient (linear/radial) fill, a batchable pen, a FULL ellipse. Mirrors EllipseRenderUnit.IsGradientBatchable.
    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(EllipsePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not GradientBrush g) return false;
        if (g is not MeshGradientBrush && g.GradientStops.Count == 0) return false;   // mesh carries corners, not stops
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    public bool CanBatch(EllipsePayload p) => WantsBatch(p);

    public bool TryAdd(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, out var item)) return false;
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake one gradient ellipse WITHOUT appending it - see GradientRectCollector.BakeItem (the paint fast-path).
    public static bool BakeItem(EllipsePayload p, Matrix4x4F world, double opacity, int transformSlot, out GradientRectItem item)
    {
        item = default;
        if (p.Brush is not GradientBrush g) return false;
        // cornerRadius = 0 (unused by the ellipse branch); shape = 1 selects the ellipse SDF in the shared gradient shader.
        return GradientRectCollector.BakeGradientItem(g, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, p.Pen, world, opacity, 1f, transformSlot, out item);
    }
}
