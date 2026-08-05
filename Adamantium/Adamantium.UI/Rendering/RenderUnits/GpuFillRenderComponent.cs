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

// GPU fill anti-aliasing (Analytic AA). A CLOSED fill contour gets a coverage fringe RING around it (FringeGeometry),
// drawn on TOP of the CPU-triangulated solid body with alpha *= coverage -> an analytic feathered edge, no MSAA. A
// geometry with holes / combined contours contributes one ring per contour, all in one vertex buffer = one draw.
//
// This is the PER-UNIT fringe: the fill it feathers isn't in the instanced path (a gradient/pattern fill, or a mesh with
// no frozen snapshot). An instanced solid fill gets the very same ring drawn once for all its elements - see
// InstancedFillCollector. Both build it from FringeGeometry, so the ring has ONE definition.
//
// The ring holds no width: the vertex shader offsets it in DEVICE PIXELS, so it is identical at any zoom - which is
// why it is built once on the CPU here rather than re-expanded on the GPU each time the scale moves.
public sealed class GpuFillRenderComponent : UIRenderComponent
{
    // Fringe width in DEVICE pixels, applied by the vertex shader.
    private const float DeviceFringePx = 1.0f;
    private const MemoryPropertyFlags FillMemory = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal;

    private readonly GraphicsDevice _device;
    private readonly FillFringeEffect _effect;
    private readonly ReusableBuffer _vertexBuffer;
    private FringeVertex[] _ring;
    private Vector4F _localBounds;   // shape local bounds (minX, minY, sizeX, sizeY): the fragment->gradient uv basis

    private ulong RingBytes => (ulong)(_ring.Length * Marshal.SizeOf<FringeVertex>());

    // The fill brush, read LIVE at Render (like the body's GeometryRenderComponent.Background) so an in-place colour
    // change or a cheap brush repoint shows without rebuilding the contour. A non-solid brush => the fringe doesn't draw
    // (the CPU body doesn't render non-solid fills either), so no stale outline is left when a fill turns into a gradient.
    public Brush Brush { get; set; }

    public GpuFillRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, FillFringeEffect effect,
        IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Brush brush, GpuBufferManager bufferManager) : base(device, uiBasicEffect, null, bufferManager)
    {
        _device = (GraphicsDevice)device;
        _effect = effect;
        Brush = brush;

        _ring = FringeGeometry.BuildRing(FringeGeometry.Build(contours));
        _localBounds = ComputeLocalBounds(contours);

        // Rented from the buffer manager (ReusableBuffer), not allocated per frame: a same-size geometry change (a
        // resize) rewrites the ring into the existing slot with no Vulkan allocation. See GPU_BUFFER_REUSE_PLAN.
        _vertexBuffer = ToDispose(BufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, FillMemory));
        _vertexBuffer.Reserve(RingBytes);
        _vertexBuffer.Invalidate();
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

    // Same-topology update (a resize): a re-tessellation that yields a ring of the SAME size rewrites it into the
    // existing slot - no new component, no allocation. Returns false when the size differs (a contour added/removed, or
    // a CornerRadius change altered an arc's segment count), so the caller rebuilds the component.
    public bool TryUpdateContours(IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours, Brush brush)
    {
        var ring = FringeGeometry.BuildRing(FringeGeometry.Build(contours));
        if (ring.Length != _ring.Length) return false;

        _ring = ring;
        _vertexBuffer.Invalidate();   // re-upload the ring to the current slot
        Brush = brush;
        _localBounds = ComputeLocalBounds(contours);
        return true;
    }

    // Upload runs out of the render pass (beforeRenderPass hook) and only when this frame's slot is stale - a static
    // fill settles to zero work. The ring itself carries no scale, so a zoom or a transform change never invalidates
    // it; only a contour change does.
    public override void PreRender()
    {
        if (!AnalyticAa.Enabled || _ring.Length == 0) return;
        if (Brush is not (SolidColorBrush or GradientBrush or PatternBrush or NoiseBrush)) return;

        var vertices = _vertexBuffer.Acquire(RingBytes, out var write);
        if (write) vertices.SetData(_ring, 0, (uint)_ring.Length);
    }

    public override void Render()
    {
        if (!AnalyticAa.Enabled || _ring.Length == 0 || Brush is not (SolidColorBrush or GradientBrush or PatternBrush or NoiseBrush)) return;

        _effect.Projection.SetValue(RenderData.TransformMatrix * RenderData.ProjectionMatrix);
        // The VS offsets the ring in DEVICE pixels, so it needs the render target's pixel size (the viewport already
        // carries the designer zoom: it is sized ClientSize x RenderScale while the projection stays logical).
        var vp = _device.CurrentViewports;
        if (vp is { Length: > 0 }) _effect.ViewportSize.SetValue(new Vector2F(vp[0].Width, vp[0].Height));
        _effect.FringePixels.SetValue(DeviceFringePx);
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

        _device.SetVertexBuffer(_vertexBuffer.Acquire(RingBytes, out _));   // the slot PreRender uploaded this frame
        _device.Draw((uint)_ring.Length, 1);
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
