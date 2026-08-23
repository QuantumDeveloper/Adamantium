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
    /// <summary>Per-instance transform RELATIVE to the transform-table slot in <see cref="Params"/> (element local space
    /// -> slot space). Row-vector convention. The vertex shader applies the slot matrix on top, so the world is never
    /// stored here and moving the slot's node does not touch this record.</summary>
    public Matrix4x4F Local;

    /// <summary>Straight-alpha RGBA (opacity already folded into A by the producer).</summary>
    public Vector4F Color;

    /// <summary>.x = transform-table slot; .y = OPACITY SLOT, sent but NOT YET READ; .zw spare.
    /// <para>Reading it in InstancedFillVS drew the fills wrong and reading it in the FRINGE VS blanked the window
    /// outright - the same driver limit the glyph batch hit (see GlyphItem.Params). The field travels so that turning
    /// it on is one line in each shader once the driver is fixed; until then these fills take the opacity CHAIN in
    /// their colour, as before.</para></summary>
    public Vector4F Params;

    public static GeometryInstance FromLocal(Matrix4x4F local, Vector4F color, int transformSlot, int fadeSlot) => new()
    {
        // Store the world matrix RAW (row-major, as C# lays out Matrix4x4F). Uniform matrices go through
        // EffectParameter.CopyMatrixColumnMajor (transpose) because cbuffers read column-major, but a matrix in a BDA
        // STORAGE buffer is read row-major by the vertex shader (Slang's default structured layout) - so the raw C#
        // bytes are already the right layout. Transposing here mis-places the translation into the 4th column, so mul()
        // dumps it into .w and every triangle collapses toward clip origin (the diagonal-streak-from-corner artifact).
        Local = local,
        Color = color,
        Params = new Vector4F(transformSlot, fadeSlot, 0, 0)
    };
}
