using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Core.Models;

/// <summary>
/// Flat, serializable snapshot of a <see cref="Mesh"/>'s geometry. Produced and consumed by
/// <see cref="MeshSerializationExtensions"/> and persisted as part of a baked model (.aemf).
/// Topology is stored as the underlying <see cref="PrimitiveTopology"/> (the <see cref="PrimitiveType"/>
/// wrapper has no public members for contractless serialization). Derived data not stored here
/// (Bounds, Semantic) is recomputed when the mesh is rebuilt.
/// </summary>
public class MeshGeometry
{
    public string Name { get; set; }
    public PrimitiveTopology MeshTopology { get; set; }
    public UpAxis UpAxis { get; set; }
    public string MaterialID { get; set; }

    public Vector3[] Points { get; set; }
    public int[] Indices { get; set; }
    public Vector3F[] Normals { get; set; }
    public Vector2F[] UV0 { get; set; }
    public Vector2F[] UV1 { get; set; }
    public Vector2F[] UV2 { get; set; }
    public Vector2F[] UV3 { get; set; }
    public Vector4F[] Tangents { get; set; }
    public Vector3F[] BiTangents { get; set; }
    public Color[] Colors { get; set; }
    public Vector4F[] JointIndices { get; set; }
    public Vector4F[] JointWeights { get; set; }
}
