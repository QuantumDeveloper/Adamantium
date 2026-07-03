using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Models;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;
using Adamantium.UI.Effects.Generated;
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// GPU consumer of the <see cref="GeometryInstanceRegistry"/>: draws each <see cref="GeometryInstanceRegistry.Segment"/>
/// as ONE instanced <c>InstancedFill</c> draw. Per segment it keeps its GPU buffers - the vertex/index buffers built
/// ONCE from the segment's shared LOCAL mesh, plus an SSBO holding the packed per-instance data (world matrix + colour)
/// that is re-uploaded only over its dirty range. So N identical shapes cost one draw, and a move/recolour is a one-slot
/// SSBO patch, not a re-tessellation. Fed by render units in a later phase; while the registry is empty this is a no-op.
/// </summary>
internal sealed class RetainedGeometryRenderer : DeferredDisposableObject
{
    // A/B gate for the retained-instancing path (RETAINED_INSTANCING=1). Off => every fill draws per-unit as before, so
    // the instanced output can be snapshot-compared against the proven path.
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("RETAINED_INSTANCING") == "1";

    private const MemoryPropertyFlags Mem = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal;

    private static readonly int VertexStride = Marshal.SizeOf<UIVertex>();
    private static readonly int InstanceStride = Marshal.SizeOf<GeometryInstance>();

    // Per-key GPU state, reused across frames. The mesh buffers are immutable once uploaded; only the SSBO changes.
    private sealed class GpuSegment
    {
        public ReusableBuffer Vtx;
        public ReusableBuffer Idx;
        public ReusableBuffer Ssbo;
        public Buffer VtxBuffer;
        public Buffer IdxBuffer;
        public uint IndexCount;
        public PrimitiveType Topology;
        public bool MeshUploaded;
    }

    private readonly GraphicsDevice _device;
    private readonly GpuBufferManager _bufferManager;
    private readonly BatchEffect _effect;
    private readonly Dictionary<GeometryKey, GpuSegment> _gpu = new();

    public RetainedGeometryRenderer(IGraphicsDevice device, GpuBufferManager bufferManager) : base(device)
    {
        _device = (GraphicsDevice)device;
        _bufferManager = bufferManager;
        _effect = new BatchEffect(device);
    }

    /// <summary>Draws every non-empty segment in <paramref name="registry"/> - one instanced draw each. Leaves the
    /// device on whatever pipeline state InstancedFill needs; the caller owns the scissor.</summary>
    public void Draw(GeometryInstanceRegistry registry, Matrix4x4F projection)
    {
        // Shared InstancedFill state (all segments draw the same way; only the mesh topology varies per segment).
        _effect.Projection.SetValue(projection);
        _device.VertexType = typeof(UIVertex);
        _device.PolygonMode = PolygonMode.Fill;
        _device.ColorBlendEnabled = true;
        _device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        _device.DepthTestEnabled = true;
        _device.DepthWriteEnable = true;
        _device.DepthCompareFunction = CompareOp.Always;

        foreach (var segment in registry.Segments)
        {
            var count = segment.Instances.Count;
            if (count == 0) continue;

            var gpu = GetOrCreate(segment);
            if (gpu == null) continue;   // no drawable mesh yet

            _effect.Instances.SetResource(UploadInstances(gpu, segment.Instances, count));
            _device.PrimitiveTopology = gpu.Topology;
            _effect.InstancedFillDrawPass.Apply();
            _device.DrawIndexed(gpu.VtxBuffer, gpu.IdxBuffer, instanceCount: (uint)count, indexCount: gpu.IndexCount);
        }
    }

    // Build (once) the shared vtx/idx buffers for a segment's local mesh. Returns null until the segment has a drawable
    // mesh (its Mesh is a Mesh with indices) - an empty/released segment is simply skipped this frame.
    private GpuSegment GetOrCreate(GeometryInstanceRegistry.Segment segment)
    {
        if (_gpu.TryGetValue(segment.Key, out var gpu) && gpu.MeshUploaded) return gpu;

        if (segment.Mesh is not Mesh mesh) return null;
        var vertices = mesh.ToUIVertices();
        var indices = mesh.Indices;
        if (indices is not { Length: > 0 } || vertices.Length == 0) return null;

        gpu ??= new GpuSegment();
        gpu.Vtx ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, Mem));
        gpu.Idx ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.IndexBuffer, Mem));
        gpu.Ssbo ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.StorageBuffer, Mem));

        gpu.VtxBuffer = gpu.Vtx.Acquire((ulong)(vertices.Length * VertexStride), out var writeV);
        if (writeV) gpu.VtxBuffer.SetData(vertices, 0, (uint)vertices.Length);
        gpu.IdxBuffer = gpu.Idx.Acquire((ulong)(indices.Length * sizeof(int)), out var writeI);
        if (writeI) gpu.IdxBuffer.SetData(indices, 0, (uint)indices.Length);

        gpu.IndexCount = (uint)indices.Length;
        gpu.Topology = mesh.MeshTopology;
        gpu.MeshUploaded = true;
        _gpu[segment.Key] = gpu;
        return gpu;
    }

    // Push the packed instance data to the SSBO. A resize (or first use) re-uploads all; otherwise only the dirty slot
    // range since the last frame (a move/recolour touches one slot; dragging the whole grid touches the whole range).
    private static Buffer UploadInstances(GpuSegment gpu, InstanceBuffer<GeometryInstance> instances, int count)
    {
        var ssbo = gpu.Ssbo.Acquire((ulong)(count * InstanceStride), out var resized);
        if (resized)
        {
            ssbo.SetData(instances.Span);
        }
        else if (instances.HasDirty)
        {
            var start = instances.DirtyStart;
            ssbo.SetData(instances.Span.Slice(start, instances.DirtyCount), (uint)(start * InstanceStride));
        }
        instances.ClearDirty();
        return ssbo;
    }
}
