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

    // Feed the shared noise-flow clock to the shader before drawing (an animated NoiseBrush reads Time to orbit its Worley
    // feature points; a static pattern/noise ignores it). NoiseClock advances only while an animating noise brush is live,
    // so Time is 0 otherwise. Same hook the fractal pass uses.
    protected override void DrawSegment(IGraphicsDevice device, Buffer<PatternRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        Effect.Time.SetValue((float)NoiseClock.Time);
        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    // Batchable = a PROCEDURAL fill (PatternBrush or NoiseBrush - both bake into this pass), a batchable pen (none or a
    // solid stroke the SDF shader draws). The four corners are independent - each rides in the record. Mirrors GradientRectCollector.CanBatch.
    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RectanglePayload p)
    {
        if (!Enabled)
        {
            return false;
        }
        if (p.Brush is not (PatternBrush or NoiseBrush))
        {
            return false;
        }
        if (!RectBatchCollector.IsPenBatchable(p.Pen))
        {
            return false;
        }
        return true;
    }

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

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

    // Bake a procedural rounded-rect fill (thin wrapper over the shared core).
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, out PatternRectItem item)
        => BakeItemCore(p.Brush, p.DestinationRect, p.CornerRadius, false, p.Pen, world, opacity, transformSlot, out item);

    // Ellipse variant: a full ellipse with a procedural fill batches into the SAME pattern pass (SDF self-AA, no jagged
    // tessellated edges) - the shader branches to the ellipse SDF on the NEGATIVE baked corner radius. Mirrors
    // GradientEllipseCollector.CanBatch/TryAdd but reuses this collector so no separate batch lifecycle is needed.
    /// <summary>THE one statement for the ellipse form - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatchEllipse(EllipsePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not (PatternBrush or NoiseBrush)) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    public bool CanBatchEllipse(EllipsePayload p) => WantsBatchEllipse(p);

    public bool TryAddEllipse(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!BakeItemCore(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, true, p.Pen, world, opacity, transformSlot, out var item))
        {
            return false;
        }
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake a procedural fill (PatternBrush or NoiseBrush) into an instance record - shared by the rect AND ellipse pattern
    // collectors. Position -> world; the pattern is fill-relative, so one brush paints any size. False on a rotated/sheared
    // world (the axis-aligned instance can not hold it). Cell scales by the world device scale (sx). The four corner radii
    // ride in Radii; Params.x carries the LARGEST of them, or -1 as the ELLIPSE shape flag (PatternPS branches SdEllipse
    // for it).
    public static bool BakeItemCore(Brush brush, Rect destinationRect, ProceduralGeometry.CornerRadius corners, bool ellipse, Pen pen, Matrix4x4F world, double opacity, int transformSlot, out PatternRectItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> per-unit
        }

        if (!PatternBrushRecord.TryDescribe(brush, out var brushRecord))
        {
            return false;
        }

        var type = brushRecord.Type;
        var color1 = brushRecord.Color1;
        var color2 = brushRecord.Color2;
        var midColor = brushRecord.MidColor;
        var cell = brushRecord.Cell;
        var brushOpacity = brushRecord.Opacity;
        var noise = brushRecord.Noise;

        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var alpha = (float)(opacity * brushOpacity);

        var c1 = color1.ToVector4();
        c1.W *= alpha;
        var c2 = color2.ToVector4();
        c2.W *= alpha;
        var c3 = midColor.ToVector4();
        c3.W *= alpha;

        RectBatchCollector.BakeStroke(pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1, out var dash);

        var r = destinationRect;
        var radii = ellipse ? Vector4F.Zero : RectBatchCollector.BakeRadii(corners, r, sx);
        item = new PatternRectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F(ellipse ? -1f : RectBatchCollector.MaxOf(radii), type, (float)(cell * sx), transformSlot),
            Radii = radii,
            Color1 = c1,
            Color2 = c2,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1,
            Dash = dash,
            Noise = noise,
            Color3 = c3,
            Anim = new Vector4F((float)brushRecord.PhaseOffset, (float)brushRecord.FrozenPhase, 0, 0)
        };
        return true;
    }
}
