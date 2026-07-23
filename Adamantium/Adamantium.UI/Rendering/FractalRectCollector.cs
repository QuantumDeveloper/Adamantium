using System;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Fractal rounded-rect batch: draws MANY rounded-rect fills whose fill is an escape-time fractal (Julia/Mandelbrot) in ONE
// instanced draw (each fill = one per-instance FractalRectItem; the pixel shader reconstructs the rounded rect from an SDF
// AND iterates the fractal per fragment). A sibling of the pattern/gradient SDF collectors - a FractalBrush fill routes
// here. Segment/buffer/overlap/retain machinery comes from SdfBatchCollector; this adds the fractal bake + the Fractal pass.
internal sealed class FractalRectCollector : SdfBatchCollector<FractalRectItem>
{
    public static bool Enabled = true;

    public FractalRectCollector() : base(256) { }

    protected override IEffectPass DrawPass => Effect.BatchFractalPass;

    // Feed the shared morph clock to the shader before drawing (only the fractal pass reads Time): a static fractal ignores
    // it; an Animate one drifts C by it. FractalClock advances only while an animating fractal is live, so this is 0 otherwise.
    protected override void DrawSegment(IGraphicsDevice device, Buffer<FractalRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        Effect.Time.SetValue((float)FractalClock.Time);
        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    // Batchable = a FractalBrush fill, a batchable pen (none or a solid stroke the SDF shader draws), and uniform corner
    // radius. Mirrors PatternRectCollector.CanBatch.
    public bool CanBatch(RectanglePayload p)
    {
        if (!Enabled)
        {
            return false;
        }
        if (p.Brush is not FractalBrush)
        {
            return false;
        }
        if (!RectBatchCollector.IsPenBatchable(p.Pen))
        {
            return false;
        }
        var c = p.CornerRadius;
        return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight && c.BottomRight == c.BottomLeft;
    }

    // Bake one fractal rounded-rect fill. False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow) -
    // the caller draws it per-unit (the demo stays axis-aligned).
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!BakeItem(p, world, opacity, transformSlot, out var item))
        {
            return false;
        }
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake a fractal fill into an instance record. Position -> world; the fractal maps the fragment to the complex plane
    // (centre/zoom are complex-plane values, NOT scaled by the device scale - only the corner radius + stroke are px).
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, out FractalRectItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> per-unit
        }
        if (p.Brush is not FractalBrush f)
        {
            return false;
        }

        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var alpha = (float)(opacity * f.Opacity);

        var c1 = f.Color1.ToVector4();
        c1.W *= alpha;
        var c2 = f.Color2.ToVector4();
        c2.W *= alpha;

        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1);

        var r = p.DestinationRect;
        item = new FractalRectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F((float)(p.CornerRadius.TopLeft * sx), (int)f.Fractal, transformSlot, f.Iterations),
            Geom = new Vector4F((float)f.Center.X, (float)f.Center.Y, (float)f.Zoom, (float)f.Power),   // .w = Multibrot exponent (MorphSpeed lives on FractalClock now)
            Julia = new Vector4F((float)f.C.X, (float)f.C.Y, f.Animate ? 1f : 0f, (int)f.Formula),
            Color1 = c1,
            Color2 = c2,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1
        };
        return true;
    }
}
