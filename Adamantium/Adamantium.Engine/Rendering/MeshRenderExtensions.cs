using System;
using Adamantium.ECS.Components;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Vertices;

namespace Adamantium.Engine.Rendering;

// The bridge between a MeshData component and the render layer: maps the component's explicit render config to a vertex
// format + device render state, and draws it through the geometry cache. Centralised so the processor and the icon
// managers share one mapping.
public static class MeshRenderExtensions
{
    public static Type ResolveVertexType(this MeshData data) =>
        data.RenderMode == MeshRenderMode.Skinned ? typeof(SkinnedMeshVertex) : typeof(MeshVertex);

    public static RenderState ToRenderState(this MeshData data) =>
        new(data.IsWireFrame, data.CullMode, data.DepthTestEnabled, data.DepthWriteEnabled, data.TopologyOverride);

    // Draws the mesh through the cache. The caller applies the effect pass first (the pass family follows the same
    // RenderMode this uses to pick the vertex format).
    public static void DrawMesh(this MeshGeometryCache cache, IGraphicsDevice device, MeshData data)
    {
        if (data?.Mesh == null) return;
        cache.GetOrCreate(data.Mesh, data.ResolveVertexType()).Draw(device, data.ToRenderState());
    }
}
