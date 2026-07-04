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
    // PARKED: the instanced-fill GPU path is a work-in-progress that does not render yet (valid pipeline/shaders/buffer/
    // instance data, no validation errors, but zero fragments - see RENDER notes). Off = every fill draws per-unit as
    // before (correct output). Flip to true to resume the WIP - the non-indexed feed + demo + gated diagnostics are ready.
    public static readonly bool Enabled = false;

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
        public uint VertexCount;
        public uint IndexCount;
        public bool Indexed;   // false = non-indexed triangle list (Draw), true = indexed (DrawIndexed)
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
        if (!_projDumped)   // TEMP: is the Projection reaching this draw a valid window ortho, or zero/garbage?
        {
            _projDumped = true;
            Console.WriteLine($"[PROJDUMP] M11={projection.M11:F5} M22={projection.M22:F5} M33={projection.M33:F5} " +
                $"M41={projection.M41:F3} M42={projection.M42:F3} M44={projection.M44:F3}");
        }
        _device.VertexType = typeof(UIVertex);
        _device.PolygonMode = PolygonMode.Fill;
        _device.RasterizerDiscardEnabled = false;   // this pass RASTERISES - a prior compute pass (fringe/stroke expander) may have left discard ON
        _device.CullMode = CullModeFlagBits.None;    // 2D fills: never cull (tessellated winding is arbitrary)
        _device.ColorBlendEnabled = true;
        _device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        _device.DepthTestEnabled = true;
        _device.DepthWriteEnable = true;
        _device.DepthCompareFunction = CompareOp.Always;

        var __drawnSegs = 0; var __totalInst = 0;   // TEMP diagnostics
        foreach (var segment in registry.Segments)
        {
            var count = segment.Instances.Count;
            if (count == 0) continue;

            var gpu = GetOrCreate(segment);
            if (gpu == null) continue;   // no drawable mesh yet

            if (__dumpKeys.Add(segment.Key) && count > 0)   // TEMP: dump each distinct segment's first instance once
            {
                var inst = segment.Instances.Span[0];
                Console.WriteLine($"[INSTDUMP] count={count} verts={gpu.VertexCount} indexed={gpu.Indexed} " +
                    $"World[M11={inst.World.M11:F2} M22={inst.World.M22:F2} M41={inst.World.M41:F1} M42={inst.World.M42:F1}] " +
                    $"Color=({inst.Color.X:F2},{inst.Color.Y:F2},{inst.Color.Z:F2},{inst.Color.W:F2})");
            }

            _effect.InstancesAddress.SetValue(UploadInstances(gpu, segment.Instances, count).GetDeviceAddress());
            // Match the WORKING RectBatch order: bind the vertex buffer + topology BEFORE Apply, then a plain Draw. Apply
            // sends the push-data (Projection / InstancesAddress) as its LAST act; binding the vertex buffer AFTER Apply
            // (the earlier Draw(buffer,...) form) reset that push data, so the shader read zeroes and the geometry
            // collapsed to a point - a valid draw that emitted no fragments (no validation error).
            _device.SetVertexBuffer(gpu.VtxBuffer);
            _device.PrimitiveTopology = gpu.Topology;
            _effect.InstancedFillDrawPass.Apply();
            if (gpu.Indexed)
                _device.DrawIndexed(gpu.VtxBuffer, gpu.IdxBuffer, instanceCount: (uint)count, indexCount: gpu.IndexCount);
            else
                _device.Draw(gpu.VertexCount, (uint)count);
            __drawnSegs++; __totalInst += count;
        }
        if (__drawnSegs != _lastSegs || __totalInst != _lastInst)   // TEMP: log only when the shape changes
        {
            _lastSegs = __drawnSegs; _lastInst = __totalInst;
            Console.WriteLine($"[INSTANCING] segments(draws)={__drawnSegs} totalInstances={__totalInst}");
        }
    }

    private int _lastSegs = -1, _lastInst = -1;   // TEMP diagnostics
    private readonly HashSet<GeometryKey> __dumpKeys = new();   // TEMP
    private bool _projDumped;   // TEMP

    // Build (once) the shared vtx/idx buffers for a segment's local mesh. Returns null until the segment has a drawable
    // mesh (its Mesh is a Mesh with indices) - an empty/released segment is simply skipped this frame.
    private GpuSegment GetOrCreate(GeometryInstanceRegistry.Segment segment)
    {
        if (_gpu.TryGetValue(segment.Key, out var gpu) && gpu.MeshUploaded) return gpu;

        if (segment.Mesh is not Mesh mesh) return null;
        var vertices = mesh.ToUIVertices();
        if (vertices.Length == 0) return null;
        if (vertices.Length >= 3)   // TEMP: verify the CPU vertex data that gets uploaded (the buffer's source)
            Console.WriteLine($"[VTXDUMP] verts={vertices.Length} stride={VertexStride} " +
                $"v0=({vertices[0].Position.X:F1},{vertices[0].Position.Y:F1},{vertices[0].Position.Z:F1}) " +
                $"v1=({vertices[1].Position.X:F1},{vertices[1].Position.Y:F1}) v2=({vertices[2].Position.X:F1},{vertices[2].Position.Y:F1})");
        var indices = mesh.Indices;
        var indexed = indices is { Length: > 0 };   // UI shape fills tessellate to a NON-indexed list; support both

        gpu ??= new GpuSegment();
        gpu.Vtx ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, Mem));
        gpu.Ssbo ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem));

        gpu.VtxBuffer = gpu.Vtx.Acquire((ulong)(vertices.Length * VertexStride), out var writeV);
        if (writeV) gpu.VtxBuffer.SetData(vertices, 0, (uint)vertices.Length);
        gpu.VertexCount = (uint)vertices.Length;

        gpu.Indexed = indexed;
        if (indexed)
        {
            gpu.Idx ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.IndexBuffer, Mem));
            gpu.IdxBuffer = gpu.Idx.Acquire((ulong)(indices.Length * sizeof(int)), out var writeI);
            if (writeI) gpu.IdxBuffer.SetData(indices, 0, (uint)indices.Length);
            gpu.IndexCount = (uint)indices.Length;
        }

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
