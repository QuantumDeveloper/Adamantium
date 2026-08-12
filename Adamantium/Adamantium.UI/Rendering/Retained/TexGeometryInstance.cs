using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// One instance of a TEXTURED fill on a shared tessellated mesh - the textured sibling of
/// <see cref="PatternGeometryInstance"/>. Matches <c>TexGeomData</c> in BatchEffect.fx field for field; read by the
/// vertex/pixel shader through a buffer device address, indexed by SV_InstanceID.
/// <para>The TEXTURE is not in the record: one is bound per draw, the way the SDF textured batch binds one per segment.
/// The engine has no bindless path, so a texture change simply splits the draw.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TexGeometryInstance
{
    /// <summary>Per-instance transform RELATIVE to the transform-table slot in <see cref="Params"/>.w (element local ->
    /// slot space). The vertex shader applies the slot matrix on top, so moving the slot's node never touches this.</summary>
    public Matrix4x4F Local;

    /// <summary>.x = clip flag (1 = ONE copy that must not spill outside its drawn rect - Uniform / None);
    /// .w = transform-table slot. .y/.z unused.</summary>
    public Vector4F Params;

    /// <summary>The shape's local-space bounds (minX, minY, sizeX, sizeY) - the box the picture is mapped across.</summary>
    public Vector4F LocalBounds;

    /// <summary>The drawn rect INSIDE that box: (offsetX, offsetY, scaleX, scaleY), each in 0..1 of the box. Only
    /// Uniform and None shrink it; everything else draws across the whole box.</summary>
    public Vector4F Drawn;

    /// <summary>The sub-rectangle of the source one copy samples.</summary>
    public Vector4F UvRect;

    /// <summary>.xy copies per axis, .z mirror flags (1 = X, 2 = Y).</summary>
    public Vector4F UvRepeat;

    public Vector4F Tint;
}
