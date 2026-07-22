using System;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Pattern rounded-rect batch: draws MANY rounded-rect fills whose fill is a PROCEDURAL two-colour pattern (checkerboard/
// stripes/dots/grid) in ONE instanced draw (each fill = one per-instance PatternRectItem; the pixel shader reconstructs the
// rounded rect from an SDF AND evaluates the pattern per fragment). A sibling of the solid/gradient SDF collectors - a
// PatternBrush fill routes here. Segment/buffer/overlap/retain machinery comes from SdfBatchCollector; this adds the
// pattern bake + the Pattern draw pass. Up to PatternType's four patterns; the second colour + cell size ride the record.
internal sealed class PatternRectCollector : SdfBatchCollector<PatternRectItem>
{
    public static bool Enabled = true;

    public PatternRectCollector() : base(512) { }

    protected override IEffectPass DrawPass => Effect.BatchPatternPass;

    // Batchable = a PatternBrush fill, a batchable pen (none or a solid stroke the SDF shader draws), and uniform corner
    // radius. Mirrors GradientRectCollector.CanBatch.
    public bool CanBatch(RectanglePayload p)
    {
        if (!Enabled)
        {
            return false;
        }
        if (p.Brush is not PatternBrush)
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

    // Bake one pattern rounded-rect fill. False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow
    // this frame) - the caller draws it per-unit (pattern per-unit not yet supported; the demo stays axis-aligned).
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

    // Bake a pattern fill into an instance record. Position -> world; the pattern is fill-relative, so one brush paints any
    // size. False on a rotated/sheared world (the axis-aligned instance can't hold it). Cell size scales by the world
    // device scale (sx), matching how corner radius + stroke width are baked into device px.
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, out PatternRectItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> per-unit
        }
        if (p.Brush is not PatternBrush pat)
        {
            return false;
        }

        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var alpha = (float)(opacity * pat.Opacity);

        var c1 = pat.Color1.ToVector4();
        c1.W *= alpha;
        var c2 = pat.Color2.ToVector4();
        c2.W *= alpha;

        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1);

        var r = p.DestinationRect;
        item = new PatternRectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F((float)(p.CornerRadius.TopLeft * sx), (int)pat.Pattern, (float)(pat.CellSize * sx), transformSlot),
            Color1 = c1,
            Color2 = c2,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1
        };
        return true;
    }
}
