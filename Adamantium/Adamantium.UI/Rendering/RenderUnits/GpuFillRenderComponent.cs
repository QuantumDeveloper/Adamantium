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

// GPU fill anti-aliasing (Analytic AA, Phase 1). For each CLOSED fill contour a compute shader (FillFringeExpand) builds
// a ~1px coverage fringe RING around it (written via a BDA device address), drawn on TOP of the CPU-triangulated solid
// body with alpha *= coverage -> an analytic feathered edge, no MSAA. Mirrors GpuStrokeRenderComponent (the same
// contour -> compute -> Draw pattern), but one-sided (outward) and carrying a coverage attribute. A geometry with holes
// / combined contours gets one ring per contour.
//
// Buffers are RENTED from the buffer manager (ReusableBuffer), not allocated per frame: a same-topology geometry change
// (a resize) rewrites the contour points in place and re-dispatches the expander into the existing ring slot, with no
// Vulkan allocation. See GPU_BUFFER_REUSE_PLAN.
public sealed class GpuFillRenderComponent : UIRenderComponent
{
    // Target fringe width in DEVICE pixels. The fringe is built in geometry-LOCAL units, so PreRender converts this to
    // local via the local->device scale (ComputeFringeWidth) - keeping the AA ~1 device px at any designer zoom.
    private const float DeviceFringePx = 1.0f;
    private const MemoryPropertyFlags FillMemory = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal;

    private readonly GraphicsDevice _device;
    private readonly FillFringeEffect _effect;
    private readonly List<Contour> _contours = [];
    private float _expandedFringe = float.NaN;
    private Vector4F _localBounds;   // shape local bounds (minX, minY, sizeX, sizeY): the fragment->gradient uv basis

    // The fill brush, read LIVE at Render (like the body's GeometryRenderComponent.Background) so an in-place colour
    // change or a cheap brush repoint shows without rebuilding the contour. A non-solid brush => the fringe doesn't draw
    // (the CPU body doesn't render non-solid fills either), so no stale outline is left when a fill turns into a gradient.
    public Brush Brush { get; set; }

    private sealed class Contour
    {
        public uint PointCount;
        public uint VertexCount;   // PointCount segments * 6 verts (closed loop)
        public float Winding;      // +1 / -1 so the outward miter actually points outward
        public float[] PointsData; // x,y interleaved - the CPU payload re-uploaded to the current ring slot when stale
        public ReusableBuffer PointsBuffer;
        public ReusableBuffer VertexBuffer;
        public ulong PointsBytes => (ulong)(PointsData.Length * sizeof(float));
        public ulong VertexBytes => (ulong)(VertexCount * 3 * sizeof(float));   // 3 floats/vertex: x, y, coverage
    }

    public GpuFillRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, FillFringeEffect effect,
        IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Brush brush, GpuBufferManager bufferManager) : base(device, uiBasicEffect, null, bufferManager)
    {
        _device = (GraphicsDevice)device;
        _effect = effect;
        Brush = brush;

        // Build each contour (base winding = outward from its own centroid), keeping its points for the nesting test.
        var built = new List<(Contour Contour, Vector2[] Points)>();
        foreach (var (points, _) in contours)
        {
            var c = BuildContour(points);
            if (c != null) built.Add((c, points));
        }
        AssignNesting(built);
        foreach (var (contour, _) in built) _contours.Add(contour);
        _localBounds = ComputeLocalBounds(contours);
    }

    // Bounding box of all contour points (local geometry space) as (minX, minY, sizeX, sizeY) - the gradient uv basis, so
    // the fringe evaluates the SAME gradient as the fill (which uses the geometry's local bounds).
    private static Vector4F ComputeLocalBounds(IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var (points, _) in contours)
            foreach (var p in points)
            {
                if (p.X < minX) minX = (float)p.X;
                if (p.Y < minY) minY = (float)p.Y;
                if (p.X > maxX) maxX = (float)p.X;
                if (p.Y > maxY) maxY = (float)p.Y;
            }
        if (minX > maxX) return new Vector4F(0, 0, 1, 1);
        return new Vector4F(minX, minY, Math.Max(maxX - minX, 1e-4f), Math.Max(maxY - minY, 1e-4f));
    }

    // Even-odd nesting: a contour inside an ODD number of the others is a HOLE - the fill is OUTSIDE it, so its fringe
    // must feather INWARD (toward the hole), opposite an outer contour. Winding alone can't tell them apart (the
    // tessellator emits holes with the same winding as outers), so flip holes here. A probe VERTEX is used, not the
    // centroid - a frame-shaped outer's centroid can fall inside its own hole and misclassify it.
    private static void AssignNesting(List<(Contour Contour, Vector2[] Points)> built)
    {
        foreach (var (contour, points) in built)
        {
            var nesting = 0;
            foreach (var (other, otherPoints) in built)
                if (!ReferenceEquals(other, contour) && PointInPolygon(points[0], otherPoints))
                    nesting++;
            if ((nesting & 1) == 1) contour.Winding = -contour.Winding;
        }
    }

    // Ray-cast point-in-polygon (used to find a contour's even-odd nesting depth -> hole vs outer).
    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        }
        return inside;
    }

    private Contour BuildContour(Vector2[] points)
    {
        if (points.Length < 3) return null;   // a fill contour needs at least a triangle

        var c = new Contour
        {
            PointCount = (uint)points.Length,
            VertexCount = (uint)points.Length * 6u,
            // Outward miter sign from the contour's signed area (screen space is y-down, so the sign is tuned by the
            // headless edge test: the fringe must land OUTSIDE the shape).
            Winding = SignedArea(points) >= 0 ? -1f : 1f,
            PointsData = ToFloats(points)
        };

        c.PointsBuffer = ToDispose(BufferManager.CreateBuffer(
            BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, FillMemory));
        c.PointsBuffer.Reserve(c.PointsBytes);
        c.PointsBuffer.Invalidate();

        c.VertexBuffer = ToDispose(BufferManager.CreateBuffer(
            BufferUsageFlags.VertexBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, FillMemory));
        c.VertexBuffer.Reserve(c.VertexBytes);
        c.VertexBuffer.Invalidate();
        return c;
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

    // Same-topology update (a resize): if the new contour set matches the current one (count + per-contour point count),
    // rewrite each contour's points in place and re-expand - no new component, no allocation. Returns false when the
    // topology differs (contour added/removed, or a CornerRadius change altered an arc's segment count), so the caller
    // rebuilds. Winding/nesting are preserved (a resize keeps both).
    public bool TryUpdateContours(IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Brush brush)
    {
        var valid = new List<Vector2[]>();
        foreach (var (points, _) in contours)
            if (points is { Length: >= 3 }) valid.Add(points);

        if (valid.Count != _contours.Count) return false;
        for (var i = 0; i < valid.Count; i++)
            if (valid[i].Length != _contours[i].PointCount) return false;

        for (var i = 0; i < valid.Count; i++)
        {
            var c = _contours[i];
            c.PointsData = ToFloats(valid[i]);
            c.PointsBuffer.Invalidate();    // re-upload the points to the current ring slot
            c.VertexBuffer.Invalidate();    // re-expand into the current ring slot
        }
        Brush = brush;
        _localBounds = ComputeLocalBounds(contours);
        return true;
    }

    private static double SignedArea(Vector2[] p)
    {
        double a = 0;
        for (int i = 0, n = p.Length; i < n; i++)
        {
            var j = (i + 1) % n;
            a += p[i].X * p[j].Y - p[j].X * p[i].Y;
        }
        return a * 0.5;
    }

    // DeviceFringePx expressed in geometry-LOCAL units. The contour is offset in local space; only the viewport applies
    // the designer zoom (RenderScale) while the projection stays logical, so 1 local unit spans worldScale (local->logical,
    // from the transform's 2x2 area scale) * RenderScale (logical->device) device px. Invert that so the fringe stays
    // ~1 device px at any zoom: no thickening when zoomed in, no sub-pixel under-coverage when zoomed out.
    private float ComputeFringeWidth()
    {
        var t = RenderData.TransformMatrix;
        var worldScale = (float)Math.Sqrt(Math.Abs(t.M11 * t.M22 - t.M12 * t.M21));
        var deviceScale = worldScale * (float)RenderData.RenderScale;
        return deviceScale > 1e-4f ? DeviceFringePx / deviceScale : DeviceFringePx;
    }

    // Compute runs out of the render pass (beforeRenderPass hook), same as the stroke expander: each contour ensures its
    // points are uploaded to this frame's slot, then re-dispatches the fringe expander into this frame's vertex slot only
    // when that slot is stale (a static fill settles to zero work; an animated one re-expands the current slot).
    public override void PreRender()
    {
        if (!AnalyticAa.Enabled || Brush is not (SolidColorBrush or GradientBrush or PatternBrush or NoiseBrush)) return;   // AA off / no fringe-eligible fill: skip the expander
        var fringeWidth = ComputeFringeWidth();
        if (fringeWidth != _expandedFringe)
        {
            foreach (var c in _contours) c.VertexBuffer.Invalidate();   // scale changed -> re-expand every contour
            _expandedFringe = fringeWidth;
        }

        foreach (var c in _contours)
        {
            var points = c.PointsBuffer.Acquire(c.PointsBytes, out var writePoints);
            if (writePoints) points.SetData(c.PointsData, 0, (uint)c.PointsData.Length);

            var vertices = c.VertexBuffer.Acquire(c.VertexBytes, out var writeVertices);
            if (!writeVertices) continue;   // this frame's slot already holds the expanded ring

            _effect.PointsAddress.SetValue(points.GetDeviceAddress());
            _effect.OutputAddress.SetValue(vertices.GetDeviceAddress());
            _effect.PointCount.SetValue(c.PointCount);
            _effect.FringeWidth.SetValue(fringeWidth);
            _effect.Winding.SetValue(c.Winding);
            _effect.FillFringeExpandPass.Apply();

            _device.Dispatch((c.PointCount + 63u) / 64u);

            _device.BufferBarrier(vertices,
                PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                PipelineStageFlagBits2.VertexAttributeInputBit, AccessFlagBits2.VertexAttributeReadBit);
        }
    }

    public override void Render()
    {
        if (!AnalyticAa.Enabled || _contours.Count == 0 || Brush is not (SolidColorBrush or GradientBrush or PatternBrush or NoiseBrush)) return;

        _effect.Projection.SetValue(RenderData.TransformMatrix * RenderData.ProjectionMatrix);
        if (Brush is GradientBrush g)
        {
            SetGradientUniforms(g);
            _effect.IsGradient.SetValue(1);
        }
        else if (Brush is SolidColorBrush solid)
        {
            var color = solid.Color.ToVector4();
            color.W *= (float)solid.Opacity * RenderData.Opacity;   // colour alpha x brush Opacity x element Opacity
            _effect.FillColor.SetValue(color);
            _effect.IsGradient.SetValue(0);
        }
        else   // PatternBrush / NoiseBrush: a flat representative edge colour (the 1px ring doesn't evaluate the pattern)
        {
            _effect.FillColor.SetValue(PatternFringeColor(Brush));
            _effect.IsGradient.SetValue(0);
        }

        _device.VertexType = typeof(FringeVertex);
        _device.PolygonMode = PolygonMode.Fill;
        _device.PrimitiveTopology = PrimitiveTopology.TriangleList;
        _device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        _device.DepthCompareFunction = CompareOp.Always;
        _device.DepthTestEnabled = true;
        _device.DepthWriteEnable = true;
        _effect.FillFringeDrawPass.Apply();

        foreach (var c in _contours)
        {
            var vertices = c.VertexBuffer.Acquire(c.VertexBytes, out _);   // the slot PreRender expanded this frame
            _device.SetVertexBuffer(vertices);
            _device.Draw(c.VertexCount, 1);
        }
    }

    // Push the fill gradient into the fringe Draw uniforms (packed by the shared GradientBake, same as the fill batch), so
    // the ring is coloured by the gradient at each fragment - the AA edge matches the fill instead of one flat colour.
    private void SetGradientUniforms(GradientBrush g)
    {
        var alpha = (float)(g.Opacity * RenderData.Opacity);
        Span<Vector4F> cols = stackalloc Vector4F[GradientBake.MaxStops];
        Span<float> offs = stackalloc float[GradientBake.MaxStops];
        var count = GradientBake.PackStops(g, alpha, cols, offs);
        var type = GradientBake.PackGeometry(g, out var geom0, out var geom1);

        _effect.GParams.SetValue(new Vector4F(0, type, count, (float)g.SpreadMethod));
        _effect.GGeom0.SetValue(geom0);
        _effect.GGeom1.SetValue(geom1);
        _effect.GLocalBounds.SetValue(_localBounds);
        _effect.GS0.SetValue(cols[0]); _effect.GS1.SetValue(cols[1]); _effect.GS2.SetValue(cols[2]); _effect.GS3.SetValue(cols[3]);
        _effect.GS4.SetValue(cols[4]); _effect.GS5.SetValue(cols[5]); _effect.GS6.SetValue(cols[6]); _effect.GS7.SetValue(cols[7]);
        _effect.GOff0.SetValue(new Vector4F(offs[0], offs[1], offs[2], offs[3]));
        _effect.GOff1.SetValue(new Vector4F(offs[4], offs[5], offs[6], offs[7]));
    }

    // Analytic-AA fringe colour for a procedural pattern/noise fill: the ring is 1px, so rather than evaluate the whole
    // pattern there (a heavy fringe shader), colour it with the brush's LOW colour (Color1). A procedural field is mostly its
    // background/low value, so a shape edge is dominated by Color1 - the ring blends into it instead of ringing a bright
    // midpoint. Not per-fragment exact, but smooths the edge without a highlighted rim. Alpha folds brush + element opacity.
    private Vector4F PatternFringeColor(Brush brush)
    {
        Color c1;
        double bo;
        if (brush is PatternBrush pb) { c1 = pb.Color1; bo = pb.Opacity; }
        else { var nb = (NoiseBrush)brush; c1 = nb.Color1; bo = nb.Opacity; }
        var v = c1.ToVector4();
        v.W *= (float)bo * RenderData.Opacity;
        return v;
    }
}
