using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// One instance of a shared local mesh in the geometry-instancing path (see <see cref="InstancedFillCollector"/>). The
/// mesh (vtx/idx) is shared per <see cref="GeometryKey"/>; everything that varies PER element - where it is (its world
/// transform) and its colour - lives here, one packed record per element. The collector packs these dense in a
/// per-key STORAGE buffer and a single instanced draw renders them all.
/// </summary>
/// <remarks>
/// The FULL world matrix is stored (not just the 2D affine part): the extra 32 bytes/instance are negligible and it
/// unlocks any transform the per-unit path allows - 3D, perspective, per-instance z - and, being the SAME matrix the
/// per-unit path multiplies (<c>mul(pos, world)</c>), makes the instanced draw match the per-unit output exactly rather
/// than a lossy affine reconstruction. The instanced fill shader does <c>mul(mul(float4(v.xyz,1), World), Projection)</c>,
/// Projection stays a shared uniform. The storage buffer is bound by BUFFER DEVICE ADDRESS and indexed by
/// <c>SV_InstanceID</c>, so the shared mesh stays the only vertex buffer (no mixed-rate vertex bindings).
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
        // Store the world matrix RAW (row-major, as C# lays out Matrix4x4F). Uniform matrices go through
        // EffectParameter.CopyMatrixColumnMajor (transpose) because cbuffers read column-major, but a matrix in a BDA
        // STORAGE buffer is read row-major by the vertex shader (Slang's default structured layout) - so the raw C#
        // bytes are already the right layout. Transposing here mis-places the translation into the 4th column, so mul()
        // dumps it into .w and every triangle collapses toward clip origin (the diagonal-streak-from-corner artifact).
        World = world,
        Color = color
    };
}
