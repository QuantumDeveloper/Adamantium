using System;
using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering.RenderUnits;

// GPU stroke path (Phase B/C). A compute shader (StrokeExpand) turns the source polyline + half-thickness into a
// triangle-LIST ribbon written via a BDA device address, the same frame draws it (StrokeDraw). No CPU re-tessellation
// of the 2D geometry - the CPU only uploads the raw contour once and sets uniforms per frame, so animation-heavy
// scenes don't pay CPU per stroke. Two modes:
//   - continuous: one thread per point -> miter ribbon + round-join/round-cap disc fans; fixed count, plain Draw.
//   - dash/trim:  a single thread walks the contour by arc length, cuts dash/trim pieces, writes the vertex count into
//                 a VkDrawIndirectCommand; DrawIndirect reads that GPU-decided count (zero per-frame CPU for dashes).
// A geometry can have SEVERAL contours (combined/group geometry, shapes with holes); each is expanded and drawn on its
// own - the pen (colour, thickness, dashes, caps, join) is shared, only the points/closedness differ per contour.
//
// Points + the expanded vertex ribbon are RENTED from the buffer manager (ReusableBuffer), not allocated per frame: a
// same-topology geometry change (a resize) rewrites the points in place and re-expands into the existing ring slot via
// TryUpdateGeometry, with no Vulkan allocation. See GPU_BUFFER_REUSE_PLAN.
public sealed class GpuStrokeRenderComponent : UIRenderComponent
{
    // Disc-fan subdivision for round joins (a smooth-enough circle); 0 when the contour has no round geometry.
    private const uint RoundSegmentsCount = 16;
    private const int MaxDashVertices = 8192 * 6;   // safety cap on the dash output buffer (per contour)
    private const MemoryPropertyFlags StrokeMemory = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal;

    private readonly GraphicsDevice _device;
    private readonly StrokeEffect _effect;
    private Pen _pen;                        // mutable: TryRepoint swaps it for a uniform-only pen change (animation)
    private readonly bool _useCut;          // dash and/or trim -> the single-thread DashCut kernel + DrawIndirect
    private readonly bool _hasDashes;
    private readonly uint _joinType;        // 0 miter, 1 bevel, 2 round
    private readonly List<Contour> _contours = [];

    // Expanded ring slots are reused until something they were expanded FROM changes: the scale (fringe) for a continuous
    // stroke, and additionally the cut parameters (offset/phase/trim/thickness) for a dashed one.
    private float _expandedFringe = float.NaN;
    private (double Offset, double Phase, double TrimStart, double TrimEnd, double Thickness) _expandedCut;
    private bool _cutDirty = true;

    // Per-contour GPU state. The expander runs once per contour (its own points + output buffers), so combined/group
    // geometry strokes every contour instead of falling back to the CPU path.
    private sealed class Contour
    {
        public bool IsClosed;
        public uint RoundSegments;   // 0 (no fan) or RoundSegmentsCount - depends on the join only (caps are analytic)
        public uint PointCount;
        public uint ThreadCount;     // compute threads: points (continuous) or 1 (dash, single-thread cut)
        public uint VertexCount;     // continuous: exact draw count; dash: output capacity (actual count is on GPU)
        public float[] PointsData;   // x,y interleaved - re-uploaded to the current ring slot when stale
        public ReusableBuffer PointsBuffer;
        public ReusableBuffer VertexBuffer;
        public Buffer PatternBuffer;     // dash only
        public Buffer IndirectBuffer;    // dash only (VkDrawIndirectCommand)
        public float DashScale = 1f;     // what FitDashesToContour stretched the pattern by (1 = untouched)
        public ulong PointsBytes => (ulong)(PointsData.Length * sizeof(float));
        public ulong VertexBytes => (ulong)(VertexCount * 10 * sizeof(float));   // see StrokeVertex (pos + one float4 per piece end)
    }

    public GpuStrokeRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, StrokeEffect effect,
        IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Pen pen, GpuBufferManager bufferManager) : base(device, uiBasicEffect, null, bufferManager)
    {
        _device = (GraphicsDevice)device;   // Dispatch/BufferBarrier/DrawIndirect live on the concrete device
        _effect = effect;
        _pen = pen;
        _hasDashes = pen.DashStrokeArray is { Count: > 0 };
        _useCut = _hasDashes || pen.TrimStart > 0.0 || pen.TrimEnd < 1.0;
        _joinType = MapJoin(pen.PenLineJoin);

        foreach (var (points, isClosed) in contours)
        {
            var contour = BuildContour(points, isClosed);
            if (contour != null) _contours.Add(contour);
        }
    }

    // True when newPen keeps every SIZE-affecting parameter (cut mode, join, dash pattern -> same buffer sizes + fan
    // counts). The rest (thickness, dash offset, trim values, colour, and now every CAP - they are analytic, so they
    // emit no geometry at all) are GPU uniforms, so a compatible pen can be swapped without reallocating - TryRepoint /
    // TryUpdateGeometry just re-dispatch with the new uniforms.
    private bool IsPenSizeCompatible(Pen newPen)
    {
        if (newPen?.Brush is not SolidColorBrush) return false;   // null pen (no stroke, e.g. thickness 0) or non-solid -> not repointable
        var newHasDashes = newPen.DashStrokeArray is { Count: > 0 };
        var newUseCut = newHasDashes || newPen.TrimStart > 0.0 || newPen.TrimEnd < 1.0;
        if (newUseCut != _useCut) return false;                       // continuous <-> cut changes the buffer set
        if (MapJoin(newPen.PenLineJoin) != _joinType) return false;  // join changes the corner fan size

        // Dash pattern (buffer size + content): each GetPen() makes a NEW collection, so compare by value, not ref.
        var oldDash = _pen.DashStrokeArray; var newDash = newPen.DashStrokeArray;
        var oldN = oldDash?.Count ?? 0; var newN = newDash?.Count ?? 0;
        if (oldN != newN) return false;
        for (var i = 0; i < oldN; i++) if (oldDash[i] != newDash[i]) return false;
        return true;
    }

    // Cheap pen update for animation: a size-compatible pen change (thickness/offset/trim/colour) keeps the buffers, so
    // just swap the pen and re-dispatch with the new uniforms - no per-frame realloc/re-upload (that churn is what tanks
    // FPS during stroke animation). Returns false (caller rebuilds via ProcessStrokeData) when a size-affecting param
    // changed.
    public bool TryRepoint(Pen newPen)
    {
        if (!IsPenSizeCompatible(newPen)) return false;
        _pen = newPen;
        foreach (var c in _contours) c.VertexBuffer.Invalidate();   // re-expand with the new uniforms (e.g. thickness)
        return true;
    }

    // Same-topology geometry update (a resize): if the new contours match the current ones (count + per-contour point
    // count + closedness) and the pen stays size-compatible, rewrite the points in place and re-expand - no new
    // component, no allocation. Returns false (caller rebuilds) for the dash/cut path (its output size depends on arc
    // length) or any topology/pen-size change.
    public bool TryUpdateGeometry(IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Pen pen)
    {
        if (_useCut) return false;
        if (!IsPenSizeCompatible(pen)) return false;

        var valid = new List<(Vector2[] Points, bool IsClosed)>();
        foreach (var (points, isClosed) in contours)
            if (points is { Length: >= 2 }) valid.Add((points, isClosed));

        if (valid.Count != _contours.Count) return false;
        for (var i = 0; i < valid.Count; i++)
            if (valid[i].Points.Length != _contours[i].PointCount || valid[i].IsClosed != _contours[i].IsClosed)
                return false;

        for (var i = 0; i < valid.Count; i++)
        {
            var c = _contours[i];
            c.PointsData = ToFloats(valid[i].Points);
            c.PointsBuffer.Invalidate();
            c.VertexBuffer.Invalidate();
        }
        _pen = pen;
        return true;
    }

    private static float[] ToFloats(Vector2[] points)
    {
        var floats = new float[points.Length * 2];
        for (var i = 0; i < points.Length; i++)
        {
            floats[i * 2] = (float)points[i].X;
            floats[i * 2 + 1] = (float)points[i].Y;
        }
        return floats;
    }

    // How much the dash pattern must stretch for a WHOLE number of periods to go round this closed contour. Never more
    // than half a period either way, so the dashes keep the length they were authored with to within a few percent.
    private static float FitScale(Vector2[] points, Pen pen)
    {
        var period = 0.0;
        foreach (var entry in pen.DashStrokeArray) period += entry;
        if (period <= 0) return 1f;

        var length = 0.0;
        for (var i = 0; i < points.Length; i++)
        {
            var next = i + 1 == points.Length ? 0 : i + 1;   // closed: the last segment runs back to the start
            length += (points[next] - points[i]).Length();
        }

        if (length <= 0) return 1f;

        var periods = Math.Max(1.0, Math.Round(length / period));
        return (float)(length / (periods * period));
    }

    // Builds the GPU buffers for one contour. Returns null for a degenerate contour (no thread/vertex work).
    private Contour BuildContour(Vector2[] points, bool isClosed)
    {
        if (points.Length < 2) return null;

        var c = new Contour { IsClosed = isClosed, PointCount = (uint)points.Length, PointsData = ToFloats(points) };

        // Disc segments are needed by the JOIN only: round (a disc) or bevel (a wedge) on the continuous path, round on
        // the cut path (a disc at every corner a dash crosses; a bevel wedge is written inline there). Caps need none -
        // they are a per-fragment mask now, which is also why a round cap no longer drags a 16-triangle fan onto every
        // dash of every dashed stroke.
        var needsFan = _useCut ? _joinType == 2u : _joinType != 0u;
        c.RoundSegments = needsFan ? RoundSegmentsCount : 0u;

        // Raw contour -> BDA storage buffer (the GPU reads it; dashing/trim happen on the GPU, not here).
        c.PointsBuffer = ToDispose(BufferManager.CreateBuffer(
            BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, StrokeMemory));
        c.PointsBuffer.Reserve(c.PointsBytes);
        c.PointsBuffer.Invalidate();

        if (_useCut)
        {
            c.ThreadCount = 1;   // a single thread walks the whole contour and cuts the dash/trim pieces
            c.VertexCount = DashOutputCapacity(points, isClosed, _pen, c.RoundSegments);   // worst-case piece bound

            // Pattern buffer: the real dash array, or a 1-element dummy for trim-only (PatternCount=0 -> not read).
            var patternArr = _hasDashes ? new float[_pen.DashStrokeArray.Count] : new float[1];
            // A CLOSED contour's length almost never divides by the dash pattern, and the remainder lands where the
            // contour closes - one long dash at the seam, moving as the shape resizes. Fitting stretches the pattern by
            // at most half a period so a whole number goes round; the offset is scaled to match when it is uploaded.
            c.DashScale = _hasDashes && isClosed && _pen.FitDashesToContour
                ? FitScale(points, _pen)
                : 1f;
            // Clamp each dash segment to >=1px. This CUT path turns every dash into REAL geometry, and a sub-pixel piece
            // becomes a degenerate quad that never rasterizes - the line breaks up at DashOn<1. The SDF batch is analytic
            // (per-fragment coverage) so a sub-pixel dash just fades there; only this compute path needs the floor.
            for (var i = 0; i < _pen.DashStrokeArray.Count; i++)
                patternArr[i] = Math.Max((float)_pen.DashStrokeArray[i] * c.DashScale, 1.0f);
            c.PatternBuffer = ToDispose(Buffer.New(_device, (ulong)(patternArr.Length * sizeof(float)),
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, StrokeMemory));
            var pp = c.PatternBuffer.MapMemory();
            System.Runtime.InteropServices.Marshal.Copy(patternArr, 0, (IntPtr)(nint)pp, patternArr.Length);
            c.PatternBuffer.UnmapMemory();

            c.IndirectBuffer = ToDispose(Buffer.New(_device, (ulong)(4 * sizeof(uint)),
                BufferUsageFlags.IndirectBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, StrokeMemory));
        }
        else
        {
            c.ThreadCount = c.PointCount;   // one thread per point (segment quad + join/cap fan)
            c.VertexCount = c.PointCount * (6u + c.RoundSegments * 3u);
        }

        if (c.ThreadCount == 0 || c.VertexCount == 0) return null;

        // 10 floats/vertex (see StrokeVertex): position, then one float4 per end of the piece.
        c.VertexBuffer = ToDispose(BufferManager.CreateBuffer(
            BufferUsageFlags.VertexBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, StrokeMemory));
        c.VertexBuffer.Reserve(c.VertexBytes);
        c.VertexBuffer.Invalidate();
        return c;
    }

    // Worst-case vertex count for the dash output buffer: total arc length / shortest "on" dash, plus a per-segment
    // allowance for corner splits. A piece is exactly its 6 quad verts (caps are a mask, they emit nothing), plus one
    // corner join per segment (a round disc, or up to 3 bevel/miter triangles). Capped. This is the only CPU work
    // dashing does - an O(points) length sum, no per-piece tessellation - so animated dashes cost the CPU nothing.
    private static uint DashOutputCapacity(Vector2[] pts, bool closed, Pen pen, uint roundSegments)
    {
        int n = pts.Length;
        int segs = closed ? n : n - 1;
        const long perPiece = 6;
        long joinVerts = (long)segs * (roundSegments > 0 ? roundSegments * 3 : 9);   // one join per corner

        // Trim-only (no dashes): trim clips to at most one run, so segs + 2 boundary splits is the bound.
        if (pen.DashStrokeArray is not { Count: > 0 })
            return (uint)Math.Clamp((segs + 2) * perPiece + joinVerts, 6, MaxDashVertices);

        double total = 0;
        for (var s = 0; s < segs; s++) total += (pts[(s + 1) % n] - pts[s]).Length();

        double minOn = double.MaxValue;
        for (var k = 0; k < pen.DashStrokeArray.Count; k += 2)
            if (pen.DashStrokeArray[k] > 0) minOn = Math.Min(minOn, pen.DashStrokeArray[k]);
        if (minOn is double.MaxValue or <= 0) minOn = 1;

        long pieces = (long)(total / minOn) + segs + 4;
        long verts = Math.Clamp(pieces * perPiece + joinVerts, 6, MaxDashVertices);
        return (uint)verts;
    }

    // Cap mapping for the expander shaders. Every cap renders as its true shape: 0 = flat, 1 = square, 2 = convex round,
    // 3 = convex triangle (tip), 4 = concave triangle (V notch), 5 = concave round (carved arc). The shader stretches
    // the end quad far enough for the shape (CapExtend) and CARVES it per fragment (CapSd) - no cap geometry exists.
    private static uint MapCap(PenLineCap cap) => cap switch
    {
        PenLineCap.Square => 1u,
        PenLineCap.ConvexRound => 2u,
        PenLineCap.ConvexTriangle => 3u,
        PenLineCap.ConcaveTriangle => 4u,
        PenLineCap.ConcaveRound => 5u,
        _ => 0u   // Flat
    };

    // Join mapping: 0 = miter (bisector ribbon, clamped), 1 = bevel (per-segment rectangles + corner wedge), 2 = round (rectangles + disc fan).
    private static uint MapJoin(PenLineJoin join) => join switch
    {
        PenLineJoin.Round => 2u,
        PenLineJoin.Bevel => 1u,
        _ => 0u
    };

    // AA feather width in geometry-LOCAL units (~1 device px). The ribbon is widened/feathered in local space and only
    // the viewport applies the designer zoom, so divide 1 device px by the local->device scale (transform area scale *
    // RenderScale) - keeps the AA ~1 device px at any zoom. Mirrors GpuFillRenderComponent.ComputeFringeWidth.
    private float ComputeFringe()
    {
        if (!AnalyticAa.Enabled) return 0f;   // AA off: no feather - the ribbon stays a hard +/-HalfThickness band
        var t = RenderData.TransformMatrix;
        var worldScale = (float)System.Math.Sqrt(System.Math.Abs(t.M11 * t.M22 - t.M12 * t.M21));
        var deviceScale = worldScale * (float)RenderData.RenderScale;
        return deviceScale > 1e-4f ? 1.0f / deviceScale : 1.0f;
    }

    // Compute runs OUT of the render pass: PreRender is recorded from BeginDraw's beforeRenderPass hook
    // (WindowRenderService.BeginDraw -> ForwardWindowRenderer.PreRender -> RenderUnit.PreRender -> here). Each contour
    // uploads its points to this frame's slot, then dispatches the expander into this frame's vertex slot when stale
    // (continuous static -> zero work; scale change / dash -> re-expand the current slot).
    public override void PreRender()
    {
        var fringe = ComputeFringe();
        var fringeChanged = fringe != _expandedFringe;
        _expandedFringe = fringe;

        // What the CUT is made of. Re-cutting unconditionally cost a dashed stroke a compute dispatch every frame even
        // when it stood still - the difference between 600 and 200 fps on a page of dashed shapes.
        var cut = (_pen.DashOffset, _pen.DashPhase, _pen.TrimStart, _pen.TrimEnd, _pen.Thickness);
        var cutChanged = _cutDirty || cut != _expandedCut;
        _expandedCut = cut;
        _cutDirty = false;

        foreach (var c in _contours)
        {
            var points = c.PointsBuffer.Acquire(c.PointsBytes, out var writePoints);
            if (writePoints) points.SetData(c.PointsData, 0, (uint)c.PointsData.Length);

            if (_useCut ? cutChanged || fringeChanged : fringeChanged) c.VertexBuffer.Invalidate();

            var vertices = c.VertexBuffer.Acquire(c.VertexBytes, out var writeVertices);
            if (!writeVertices) continue;   // continuous static: this slot already holds the expanded ribbon

            _effect.PointsAddress.SetValue(points.GetDeviceAddress());
            _effect.OutputAddress.SetValue(vertices.GetDeviceAddress());
            _effect.PointCount.SetValue(c.PointCount);
            _effect.IsClosed.SetValue(c.IsClosed ? 1u : 0u);
            _effect.DashMode.SetValue(_useCut ? 1u : 0u);
            _effect.JoinType.SetValue(_joinType);
            _effect.RoundSegments.SetValue(c.RoundSegments);
            // Start/EndLineCap belong to the stroke's REAL ends, and a closed contour only has them where a trim window
            // cuts one (that is what puts a round end on a progress ring). Untrimmed and closed, there is no real end
            // at all, so the seam wears the dash caps like every other dash boundary instead of a stray line cap.
            var hasRealEnds = !c.IsClosed || _pen.TrimStart > 0.0 || _pen.TrimEnd < 1.0;
            _effect.StartCap.SetValue(MapCap(hasRealEnds ? _pen.StartLineCap : _pen.DashStartCap));
            _effect.EndCap.SetValue(MapCap(hasRealEnds ? _pen.EndLineCap : _pen.DashEndCap));
            // The two ends of a dash are separate properties: one dash can be a concave bite and a convex tip - an arrow.
            _effect.DashStartCap.SetValue(MapCap(_pen.DashStartCap));
            _effect.DashEndCap.SetValue(MapCap(_pen.DashEndCap));
            _effect.HalfThickness.SetValue((float)(_pen.Thickness / 2.0));
            _effect.Fringe.SetValue(fringe);   // AA feather: the expander widens the ribbon/discs by Fringe/2

            if (_useCut)
            {
                _effect.IndirectAddress.SetValue(c.IndirectBuffer.GetDeviceAddress());
                _effect.PatternAddress.SetValue(c.PatternBuffer.GetDeviceAddress());
                _effect.PatternCount.SetValue(_hasDashes ? (uint)_pen.DashStrokeArray.Count : 0u);
                _effect.MaxVertices.SetValue(c.VertexCount);
                // The phase is in PERIODS, so it becomes pixels here - which is exactly the arithmetic the author would
                // otherwise be doing by hand. Both parts then scale with the contour fit, or the ring, seamless in
                // shape, would still drift in phase as it marches.
                var period = 0.0;
                foreach (var entry in _pen.DashStrokeArray) period += entry;
                var effectiveOffset = (float)((_pen.DashOffset + _pen.DashPhase * period) * c.DashScale);
                if (Environment.GetEnvironmentVariable("ADAM_DASH_LOG") == "1")
                {
                    Console.Error.WriteLine(
                        $"[DASH] phase={_pen.DashPhase:F3} pxOffset={_pen.DashOffset:F2} period={period:F1} " +
                        $"scale={c.DashScale:F3} -> {effectiveOffset:F2}");
                }
                _effect.DashOffset.SetValue(effectiveOffset);
                _effect.TrimStart.SetValue((float)Math.Clamp(_pen.TrimStart, 0.0, 1.0));
                _effect.TrimEnd.SetValue((float)Math.Clamp(_pen.TrimEnd, 0.0, 1.0));
                _effect.StrokeDashCutPass.Apply();   // separate compute kernel for the dash/trim cut
            }
            else
            {
                _effect.StrokeExpandPass.Apply();
            }

            _device.Dispatch((c.ThreadCount + 63) / 64);

            // Make the compute output visible to the draw: vertex-attribute read (both modes) and, for the cut, the
            // indirect-command read.
            _device.BufferBarrier(vertices,
                PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                PipelineStageFlagBits2.VertexAttributeInputBit, AccessFlagBits2.VertexAttributeReadBit);
            if (_useCut)
            {
                _device.BufferBarrier(c.IndirectBuffer,
                    PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                    PipelineStageFlagBits2.DrawIndirectBit, AccessFlagBits2.IndirectCommandReadBit);
            }
        }
    }

    public override void Render()
    {
        if (_contours.Count == 0) return;

        // Points are in geometry-local space: WVP = transform(local->world) * projection(world->clip). The pen colour
        // and transform are shared by every contour, so set them once, then draw each contour's expanded ribbon.
        _effect.Projection.SetValue(RenderData.TransformMatrix * RenderData.ProjectionMatrix);
        var color = (_pen.Brush as SolidColorBrush)?.Color.ToVector4() ?? new Vector4F(0, 0, 0, 1);
        color.W *= (float)(_pen.Brush?.Opacity ?? 1.0) * RenderData.Opacity;   // colour alpha x pen-brush Opacity x element Opacity
        _effect.StrokeColor.SetValue(color);
        // StrokePS reads these for the analytic-AA coverage (distance vs HalfThickness +/- Fringe/2).
        _effect.HalfThickness.SetValue((float)(_pen.Thickness / 2.0));
        _effect.Fringe.SetValue(ComputeFringe());

        _device.VertexType = typeof(StrokeVertex);
        _device.PolygonMode = PolygonMode.Fill;
        _device.PrimitiveTopology = PrimitiveTopology.TriangleList;
        _device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        _device.DepthCompareFunction = CompareOp.Always;
        _device.DepthTestEnabled = true;
        _device.DepthWriteEnable = true;
        _effect.StrokeDrawPass.Apply();

        foreach (var c in _contours)
        {
            var vertices = c.VertexBuffer.Acquire(c.VertexBytes, out _);   // the slot PreRender expanded this frame
            _device.SetVertexBuffer(vertices);
            if (_useCut)
                _device.DrawIndirect(vertices, c.IndirectBuffer);   // GPU-decided vertex count (dash/trim)
            else
                _device.Draw(c.VertexCount, 1);
        }
    }
}
