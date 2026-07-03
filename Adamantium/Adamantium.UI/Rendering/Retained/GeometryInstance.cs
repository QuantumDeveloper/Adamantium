using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// One instance of a shared local mesh in the retained geometry-instancing scene (docs/RENDER_CACHE_REDESIGN.md
/// §4e/§4h). The mesh (vtx/idx) is shared per <see cref="GeometryKey"/>; everything that varies PER element - where it
/// is (its world transform) and its colour - lives here, one packed record per element. The registry keeps these dense
/// in an <see cref="InstanceBuffer{T}"/> and a single instanced draw renders them all.
/// </summary>
/// <remarks>
/// The FULL world matrix is stored (not just the 2D affine part): the extra 32 bytes/instance are negligible and it
/// unlocks any transform the per-unit path allows - 3D, perspective, per-instance z - and, being the SAME matrix the
/// per-unit path multiplies (<c>mul(pos, world)</c>), makes the "retained == old path" snapshot exact rather than a
/// lossy affine reconstruction. The instanced fill shader does <c>mul(mul(float4(v.xyz,1), World), Projection)</c>,
/// Projection stays a shared uniform.
///
/// This is a STORAGE-buffer element (§4j): the dense <see cref="InstanceBuffer{T}"/> is uploaded to an SSBO bound
/// through the descriptor heap, and the instanced fill shader indexes it by <c>SV_InstanceID</c> - so the shared mesh
/// stays the only vertex buffer (no mixed-rate vertex bindings) and per-instance data can grow freely.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct GeometryInstance
{
    /// <summary>Full per-instance world transform (element local space -> world). Row-vector convention.</summary>
    public Matrix4x4F World;

    /// <summary>Straight-alpha RGBA (opacity already folded into A by the producer).</summary>
    public Vector4F Color;

    public static GeometryInstance FromWorld(Matrix4x4F world, Vector4F color) => new()
    {
        World = world,
        Color = color
    };
}
