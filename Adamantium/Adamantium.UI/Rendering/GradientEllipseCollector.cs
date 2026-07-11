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
    public bool CanBatch(EllipsePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not GradientBrush g || g.GradientStops.Count == 0) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    public bool TryAdd(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        if (p.Brush is not GradientBrush g) return false;
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        // cornerRadius = 0 (unused by the ellipse branch); shape = 1 selects the ellipse SDF in the shared gradient shader.
        if (!GradientRectCollector.BakeGradientItem(g, p.DestinationRect, 0.0, p.Pen, world, opacity, 1f, transformSlot, out var item)) return false;
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }
}
