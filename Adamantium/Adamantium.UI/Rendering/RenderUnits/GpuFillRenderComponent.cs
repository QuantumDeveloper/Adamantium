using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
public sealed class GpuFillRenderComponent : UIRenderComponent
{
    // Target fringe width in DEVICE pixels. The fringe is built in geometry-LOCAL units, so PreRender converts this to
    // local via the local->device scale (ComputeFringeWidth) - keeping the AA ~1 device px at any designer zoom.
    private const float DeviceFringePx = 1.0f;

    private readonly GraphicsDevice _device;
    private readonly FillFringeEffect _effect;
    private readonly List<Contour> _contours = [];

    // The expander output is geometry-LOCAL (only the fringe width depends on scale), so once expanded it's reused every
    // frame until the scale changes - a geometry/brush change recreates the whole component. Skipping the per-frame
    // compute+barrier for static fills is a big win (the draw still runs each frame).
    private bool _expanded;
    private float _expandedFringe = float.NaN;

    // The fill brush, read LIVE at Render (like the body's GeometryRenderComponent.Background) so an in-place colour
    // change or a cheap brush repoint shows without rebuilding the contour. A non-solid brush => the fringe doesn't draw
    // (the CPU body doesn't render non-solid fills either), so no stale outline is left when a fill turns into a gradient.
    public Brush Brush { get; set; }

    private sealed class Contour
    {
        public uint PointCount;
        public uint VertexCount;   // PointCount segments * 6 verts (closed loop)
        public float Winding;      // +1 / -1 so the outward miter actually points outward
        public Buffer PointsBuffer;
        public Buffer VertexBuffer;
    }

    public GpuFillRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, FillFringeEffect effect,
        IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Brush brush) : base(device, uiBasicEffect, null)
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

        // Even-odd nesting: a contour inside an ODD number of the others is a HOLE - the fill is OUTSIDE it, so its
        // fringe must feather INWARD (toward the hole), opposite an outer contour. Winding alone can't tell them apart
        // (the tessellator emits holes with the same winding as outers), so flip holes here. A probe VERTEX is used, not
        // the centroid - a frame-shaped outer's centroid can fall inside its own hole and misclassify it.
        foreach (var (contour, points) in built)
        {
            var nesting = 0;
            foreach (var (other, otherPoints) in built)
                if (!ReferenceEquals(other, contour) && PointInPolygon(points[0], otherPoints))
                    nesting++;
            if ((nesting & 1) == 1) contour.Winding = -contour.Winding;
            _contours.Add(contour);
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
            Winding = SignedArea(points) >= 0 ? -1f : 1f
        };

        var floats = new float[points.Length * 2];
        for (var i = 0; i < points.Length; i++)
        {
            floats[i * 2] = (float)points[i].X;
            floats[i * 2 + 1] = (float)points[i].Y;
        }
        c.PointsBuffer = ToDispose(Buffer.New(_device, (ulong)(floats.Length * sizeof(float)),
            BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal));
        var ptr = c.PointsBuffer.MapMemory();
        Marshal.Copy(floats, 0, (IntPtr)(nint)ptr, floats.Length);
        c.PointsBuffer.UnmapMemory();

        // 3 floats/vertex: (x, y, coverage) - matches FringeVertex + the shader's float3 output.
        c.VertexBuffer = ToDispose(Buffer.New(_device, (ulong)(c.VertexCount * 3 * sizeof(float)),
            BufferUsageFlags.VertexBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal));
        return c;
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

    // Compute runs out of the render pass (beforeRenderPass hook), same as the stroke expander: each contour dispatches
    // the fringe expander and barriers its output for the draw.
    public override void PreRender()
    {
        if (!AnalyticAa.Enabled || Brush is not SolidColorBrush) return;   // AA off or non-solid: skip the expander
        var fringeWidth = ComputeFringeWidth();
        if (_expanded && fringeWidth == _expandedFringe) return;   // already expanded at this scale - reuse the ring
        _expanded = true;
        _expandedFringe = fringeWidth;
        foreach (var c in _contours)
        {
            _effect.PointsAddress.SetValue(c.PointsBuffer.GetDeviceAddress());
            _effect.OutputAddress.SetValue(c.VertexBuffer.GetDeviceAddress());
            _effect.PointCount.SetValue(c.PointCount);
            _effect.FringeWidth.SetValue(fringeWidth);
            _effect.Winding.SetValue(c.Winding);
            _effect.FillFringeExpandPass.Apply();

            _device.Dispatch((c.PointCount + 63u) / 64u);

            _device.BufferBarrier(c.VertexBuffer,
                PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                PipelineStageFlagBits2.VertexAttributeInputBit, AccessFlagBits2.VertexAttributeReadBit);
        }
    }

    public override void Render()
    {
        if (!AnalyticAa.Enabled || _contours.Count == 0 || Brush is not SolidColorBrush solid) return;

        _effect.Projection.SetValue(RenderData.TransformMatrix * RenderData.ProjectionMatrix);
        var color = solid.Color.ToVector4();
        color.W *= (float)solid.Opacity * RenderData.Opacity;   // colour alpha x brush Opacity x element Opacity
        _effect.FillColor.SetValue(color);

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
            _device.SetVertexBuffer(c.VertexBuffer);
            _device.Draw(c.VertexCount, 1);
        }
    }
}
