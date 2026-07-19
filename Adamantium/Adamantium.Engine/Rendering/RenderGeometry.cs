using System;
using Adamantium.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Models;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.Engine.Rendering;

// GPU vertex/index buffers for one mesh, built for a specific vertex format. Owned by the render layer (the geometry
// cache), NOT by an ECS component - a component is bindable/cloneable per-entity editor data and must not hold GPU
// resources. Rebuilt only when the source mesh reports IsModified.
public sealed class RenderGeometry : DisposableObject
{
    private readonly Mesh _mesh;
    private IBuffer _vertexBuffer;
    private IBuffer _indexBuffer;

    public RenderGeometry(Mesh mesh, Type vertexType)
    {
        _mesh = mesh;
        VertexType = vertexType;
    }

    public Type VertexType { get; }
    public IBuffer VertexBuffer => _vertexBuffer;
    public IBuffer IndexBuffer => _indexBuffer;
    public PrimitiveTopology Topology { get; private set; }
    public bool HasIndices { get; private set; }

    // (Re)builds the buffers when they don't exist yet or the mesh changed; a no-op on an unchanged mesh (the common
    // per-frame case) - which is what keeps the re-upload off the hot path.
    public void EnsureUpToDate(IGraphicsDevice device)
    {
        if (_vertexBuffer != null && !_mesh.IsModified) return;

        if (VertexType == typeof(SkinnedMeshVertex))
            UploadVertices(device, _mesh.ToSkinnedMeshVertices());
        else
            UploadVertices(device, _mesh.ToMeshVertices());

        HasIndices = _mesh.HasIndices;
        if (HasIndices)
        {
            RemoveAndDispose(ref _indexBuffer);
            _indexBuffer = ToDispose(Buffer.Index.New(device, _mesh.Indices, BufferMemoryUsage.GpuOnly));
        }

        Topology = _mesh.MeshTopology;
        _mesh.AcceptChanges();
    }

    // Applies the per-object render state, sets the vertex input, and issues the draw. The caller applies the effect
    // pass first. The indexed path is a single DrawIndexed (it binds VB+IB itself) - no separate SetVertexBuffer/
    // SetIndexBuffer pre-bind, which is what collapses the old double bind (D2). The non-indexed path keeps its one
    // SetVertexBuffer + Draw.
    public void Draw(IGraphicsDevice device, in RenderState state)
    {
        if (_vertexBuffer == null || _vertexBuffer.IsDisposed) return;

        device.VertexType = VertexType;
        device.PrimitiveTopology = state.TopologyOverride ?? Topology;
        device.PolygonMode = state.WireFrame ? PolygonMode.Line : PolygonMode.Fill;
        device.CullMode = state.CullMode;
        device.DepthTestEnabled = state.DepthTest;
        device.DepthWriteEnable = state.DepthWrite;

        if (HasIndices)
        {
            device.DrawIndexed(_vertexBuffer, _indexBuffer);
        }
        else
        {
            device.SetVertexBuffer(_vertexBuffer);
            device.Draw(_vertexBuffer.ElementCount, 1);
        }
    }

    private void UploadVertices<T>(IGraphicsDevice device, T[] vertices) where T : struct
    {
        if (vertices == null) return;

        if (_vertexBuffer != null && (ulong)vertices.Length <= _vertexBuffer.ElementCount)
        {
            _vertexBuffer.SetData(vertices);
        }
        else
        {
            RemoveAndDispose(ref _vertexBuffer);
            _vertexBuffer = ToDispose(Buffer.Vertex.New(device, vertices, BufferMemoryUsage.GpuOnly));
        }
    }
}
