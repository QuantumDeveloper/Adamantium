using System.Collections.Generic;
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
internal sealed class PatternRectCollector : BrushSdfCollector<PatternRectItem>
{
    public static bool Enabled = true;

    public PatternRectCollector() : base(512) { }

    // ONE KIND PER SEGMENT. Each kind is its own pass now (BrushEffect.fx: technique Pattern / technique Noise), because
    // one pixel shader branching over fourteen fields was what kept the driver on the edge of refusing to create it. So
    // a segment must be uniform in kind, exactly as the textured batch's segment is uniform in texture - same three
    // hooks, same question asked by the caller before adding. Cost: two kinds in one clip group are two draws instead of
    // one, and a screen holds a handful of kinds at a time.
    private int _kind;
    private readonly List<int> _segKinds = new();

    protected override void OnBeginFrame(IGraphicsDevice device)
    {
        base.OnBeginFrame(device);
        _segKinds.Clear();
    }

    protected override void OnSegmentRecorded(int index)
    {
        while (_segKinds.Count <= index) _segKinds.Add(0);
        _segKinds[index] = _kind;
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segKinds.Count < index) _segKinds.Add(0);
        _segKinds.Insert(index, index > 0 ? _segKinds[index - 1] : 0);
    }

    protected override void BindSegment(int index) => _kind = _segKinds[index];

    /// <summary>Still the pending segment's kind? A change flushes the batch - the caller asks this before adding,
    /// mirroring TextureBatchCollector.SameTexture.</summary>
    public bool SameKind(int kind) => !Active || _kind == kind;

    /// <summary>The kind this brush bakes as, for the caller's SameKind check. -1 = not a procedural brush at all.</summary>
    public static int KindOf(Brush brush) =>
        PatternBrushRecord.TryDescribe(brush, out var record) ? record.Type : -1;

    // Pattern kinds and noise kinds share one record and one vertex stage, so they differ only in which pass runs.
    // Anything unrecognised falls back to the checkerboard pass rather than drawing nothing: a new PatternType that
    // nobody wired up should look wrong, not vanish.
    protected override IEffectPass DrawPass => _kind switch
    {
        // Patterns 0..N, noise in its own hundred (PatternBrushRecord.NoiseBase) - two ranges, not one interleaved list.
        1 => Effect.PatternStripesSdfPass,
        2 => Effect.PatternDotsSdfPass,
        3 => Effect.PatternGridSdfPass,
        4 => Effect.PatternHexagonSdfPass,
        5 => Effect.PatternHatchSdfPass,
        6 => Effect.PatternWeaveSdfPass,

        100 => Effect.NoiseSimplexSdfPass,
        101 => Effect.NoisePerlinSdfPass,
        102 => Effect.NoiseValueSdfPass,
        103 => Effect.NoiseWorleySdfPass,
        104 => Effect.NoiseRidgedSdfPass,
        105 => Effect.NoiseTurbulenceSdfPass,
        106 => Effect.NoiseVoronoiSdfPass,
        107 => Effect.NoiseCombustibleSdfPass,
        _ => Effect.PatternCheckerboardSdfPass
    };

    // Feed the shared noise-flow clock to the shader before drawing (an animated NoiseBrush reads Time to orbit its Worley
    // feature points; a static pattern/noise ignores it). NoiseClock advances only while an animating noise brush is live,
    // so Time is 0 otherwise. Same hook the fractal pass uses.
    protected override void DrawSegment(IGraphicsDevice device, Buffer<PatternRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        EnsureEffectForDraw(device);
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
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0, int fadeSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!BakeItem(p, world, opacity, transformSlot, fadeSlot, out var item))
        {
            return false;
        }
        _kind = (int)item.Params.Y;   // the pass this segment draws with; the caller has already flushed on a change
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // POLYGON variant: a regular polygon with a procedural fill batches into the SAME pass. The shape stays a distance
    // field - one instanced draw, self-anti-aliased - and only the source of the colour differs.
    /// <summary>THE one statement for the polygon form - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatchPolygon(RegularPolygonPayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not (PatternBrush or NoiseBrush)) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return !RegularPolygonCollector.NeedsArcLength(p.Pen);
    }

    public bool CanBatchPolygon(RegularPolygonPayload p) => WantsBatchPolygon(p);

    public bool TryAddPolygon(RegularPolygonPayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0, int fadeSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItemCore(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty,
                BrushShape.Polygon(p, (float)world.M11), p.Pen, world, opacity, transformSlot, fadeSlot, out var item))
        {
            return false;
        }
        _kind = (int)item.Params.Y;   // the pass this segment draws with; the caller has already flushed on a change
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake a procedural rounded-rect fill (thin wrapper over the shared core).
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot, out PatternRectItem item)
        => BakeItemCore(p.Brush, p.DestinationRect, p.CornerRadius, BrushShape.Rect, p.Pen, world, opacity, transformSlot, fadeSlot, out item);

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

    public bool TryAddEllipse(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0, int fadeSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!BakeItemCore(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, BrushShape.Ellipse, p.Pen, world, opacity, transformSlot, fadeSlot, out var item))
        {
            return false;
        }
        _kind = (int)item.Params.Y;   // the pass this segment draws with; the caller has already flushed on a change
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake a procedural fill (PatternBrush or NoiseBrush) into an instance record - shared by the rect AND ellipse pattern
    // collectors. Position -> world; the pattern is fill-relative, so one brush paints any size. False on a rotated/sheared
    // world (the axis-aligned instance can not hold it). Cell scales by the world device scale (sx). The four corner radii
    // ride in Radii; Params.x carries the LARGEST of them, or -1 as the ELLIPSE shape flag (PatternPS branches SdEllipse
    // for it).
    public static bool BakeItemCore(Brush brush, Rect destinationRect, ProceduralGeometry.CornerRadius corners, BrushShape shape, Pen pen, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot, out PatternRectItem item)
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
        var rectRadii = RectBatchCollector.BakeRadii(corners, r, sx);
        var radii = shape.RadiiFor(rectRadii);
        item = new PatternRectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F(shape.RadiusFlag(rectRadii), type, (float)(cell * sx), transformSlot),
            Radii = radii,
            Color1 = c1,
            Color2 = c2,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1,
            Dash = dash,
            Noise = noise,
            Color3 = c3,
            // .z = the opacity slot this instance reads its fade from (-1 = none); Anim's spare component rather than a
            // field of its own, which the record has no room for.
            Anim = new Vector4F((float)brushRecord.PhaseOffset, (float)brushRecord.FrozenPhase, fadeSlot, 0)
        };
        return true;
    }
}
