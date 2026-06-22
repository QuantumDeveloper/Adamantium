using System;
using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering.RenderUnits;

// GPU stroke path (Phase B): a compute shader (StrokeExpand) turns the source polyline + half-thickness into a
// miter-joined triangle-strip ribbon, written straight into a vertex buffer via a BDA device address; the same frame
// binds that buffer and draws it (StrokeDraw). No CPU re-tessellation. Handles a single contour, open OR closed
// (closed = wrap-around miters + one closing pair); solid colour. Caps / dashes still use the CPU path.
public sealed class GpuStrokeRenderComponent : UIRenderComponent
{
    private readonly GraphicsDevice _device;
    private readonly StrokeEffect _effect;
    private readonly Pen _pen;
    private readonly bool _isClosed;
    private readonly uint _pointCount;
    private readonly uint _pairCount;     // offset pairs emitted: pointCount (open) or pointCount + 1 (closed, closing pair)
    private readonly uint _vertexCount;   // pairCount * 2 (two offset verts per pair, triangle strip)
    private readonly Buffer _pointsBuffer;
    private readonly Buffer _vertexBuffer;

    public GpuStrokeRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, StrokeEffect effect,
        Vector2[] points, bool isClosed, Pen pen) : base(device, uiBasicEffect, null)
    {
        _device = (GraphicsDevice)device;   // Dispatch/BufferBarrier live on the concrete device
        _effect = effect;
        _pen = pen;
        _isClosed = isClosed;
        _pointCount = (uint)points.Length;
        _pairCount = _pointCount + (isClosed ? 1u : 0u);
        _vertexCount = _pairCount * 2;

        // float2[] points -> BDA storage buffer (compute input).
        var floats = new float[points.Length * 2];
        for (var i = 0; i < points.Length; i++)
        {
            floats[i * 2] = (float)points[i].X;
            floats[i * 2 + 1] = (float)points[i].Y;
        }
        _pointsBuffer = ToDispose(Buffer.New(_device, (ulong)(floats.Length * sizeof(float)),
            BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal));
        var ptr = _pointsBuffer.MapMemory();
        Marshal.Copy(floats, 0, (IntPtr)(nint)ptr, floats.Length);
        _pointsBuffer.UnmapMemory();

        // float2[] ribbon vertices -> BDA + vertex buffer (compute output, draw input).
        _vertexBuffer = ToDispose(Buffer.New(_device, (ulong)(_vertexCount * 2 * sizeof(float)),
            BufferUsageFlags.VertexBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal));
    }

    // Square is the only non-flat cap the strip expander handles for now; round/triangle caps take the CPU path
    // (the gating in RenderUnit.TryGetSolidPolyline rejects them), so anything not Square maps to flat (0).
    private static uint MapCap(PenLineCap cap) => cap == PenLineCap.Square ? 1u : 0u;

    // Compute runs OUT of the render pass: PreRender is recorded from BeginDraw's beforeRenderPass hook
    // (WindowRenderService.BeginDraw -> ForwardWindowRenderer.PreRender -> RenderUnit.PreRender -> here).
    public override void PreRender()
    {
        if (_pointCount < 2) return;

        _effect.PointsAddress.SetValue(_pointsBuffer.GetDeviceAddress());
        _effect.OutputAddress.SetValue(_vertexBuffer.GetDeviceAddress());
        _effect.PointCount.SetValue(_pointCount);
        _effect.IsClosed.SetValue(_isClosed ? 1u : 0u);
        // Caps only apply to open ends; closed loops have none.
        _effect.StartCap.SetValue(_isClosed ? 0u : MapCap(_pen.StartLineCap));
        _effect.EndCap.SetValue(_isClosed ? 0u : MapCap(_pen.EndLineCap));
        _effect.HalfThickness.SetValue((float)(_pen.Thickness / 2.0));
        _effect.StrokeExpandPass.Apply();

        _device.Dispatch((_pairCount + 63) / 64);

        // Compute write -> vertex-attribute read: the barrier makes the result visible to input assembly.
        _device.BufferBarrier(_vertexBuffer,
            PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
            PipelineStageFlagBits2.VertexAttributeInputBit, AccessFlagBits2.VertexAttributeReadBit);
    }

    public override void Render()
    {
        if (_pointCount < 2) return;

        // Points are in geometry-local space: WVP = transform(local->world) * projection(world->clip).
        _effect.Projection.SetValue(RenderData.TransformMatrix * RenderData.ProjectionMatrix);
        var color = (_pen.Brush as SolidColorBrush)?.Color.ToVector4() ?? new Vector4F(0, 0, 0, 1);
        color.W *= RenderData.Opacity;   // StrokeDraw has no separate opacity uniform - fold it into alpha
        _effect.StrokeColor.SetValue(color);

        _device.VertexType = typeof(StrokeVertex);
        _device.PolygonMode = PolygonMode.Fill;
        _device.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        _device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        _device.DepthCompareFunction = CompareOp.Always;
        _device.DepthTestEnabled = true;
        _device.DepthWriteEnable = true;
        _device.SetVertexBuffer(_vertexBuffer);
        _effect.StrokeDrawPass.Apply();
        _device.Draw(_vertexCount, 1);
    }
}
