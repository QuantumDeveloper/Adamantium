using System;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
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
internal sealed class RectBatchCollector : BatchCollector<RectItem>
{
    // A/B / safety-valve toggle: off routes every rect back to its per-unit fill + AA-fringe draw (the pre-batch path).
    public static bool Enabled = true;

    private RectBatchEffect _effect;

    public RectBatchCollector() : base(4096) { }

    protected override void OnBeginFrame(IGraphicsDevice device) => _effect ??= new RectBatchEffect(device);

    // Batchable = a visible solid fill, no visible pen (a border is a separate DrawGeometry), uniform corner radius.
    // Everything else (gradient/image fill, a pen, per-corner radii, Enabled=off) falls back to the per-unit draw.
    public bool CanBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not SolidColorBrush s || s.Color.A == 0) return false;
        if (p.Pen != null) return false;
        var c = p.CornerRadius;
        return c.TopLeft == c.TopRight && c.TopRight == c.BottomRight && c.BottomRight == c.BottomLeft;
    }

    // Bake one solid rounded-rect fill (position -> world, colour straight with opacity folded in) into the pending
    // segment. False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow this frame) - the
    // caller then draws that rect via the per-unit path.
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds)
    {
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;

        var solid = (SolidColorBrush)p.Brush;
        var color = solid.Color.ToVector4();
        color.W *= (float)(opacity * solid.Opacity);

        var r = p.DestinationRect;
        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        Items[Count++] = new RectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F((float)(p.CornerRadius.TopLeft * sx), 0, 0, 0),
            Color = color
        };
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Straight-alpha AlphaBlend (matches solid fills); depth like the other main-pass units (Always, test+write).
    protected override void DrawSegment(IGraphicsDevice device, Buffer<RectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        device.ColorBlendEnabled = true;
        device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        device.PrimitiveRestartEnable = true;
        device.DepthTestEnabled = true;
        device.DepthWriteEnable = true;
        device.DepthCompareFunction = CompareOp.Always;
        _effect.Projection.SetValue(projection);
        device.VertexType = typeof(RectItem);
        device.SetVertexBuffer(buffer);
        device.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        _effect.RectBatchDrawPass.Apply();
        device.Draw(4, count, 0, firstInstance);
    }
}
