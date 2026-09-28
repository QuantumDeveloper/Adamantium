namespace Adamantium.ECS.Components;

// How an entity's mesh is drawn - an EXPLICIT per-object choice, NOT derived from what the mesh happens to contain.
// The same Mesh can be rendered Static (plain geometry) or Skinned (animated), so this drives both the vertex format
// the GPU buffers are built for and the shader pass.
public enum MeshRenderMode
{
    Static,
    Skinned
}
