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

    /// <summary>.x = repeat flag (1 = the tile repeats; 0 = a single copy, which must never wrap); .y = mirror flags
    /// (1 = X, 2 = Y, 3 = both); .w = transform-table slot. .z unused.</summary>
    public Vector4F Params;

    /// <summary>The shape's local-space bounds (minX, minY, sizeX, sizeY) - the box the picture is mapped across.</summary>
    public Vector4F LocalBounds;

    /// <summary>The tile grid over that box: tiles per axis (.xy), grid origin in tiles (.zw).</summary>
    public Vector4F Tile;

    /// <summary>The content's rect inside ONE tile: (offsetX, offsetY, scaleX, scaleY), each in 0..1 of the tile. Only
    /// Uniform and None shrink it; everything else fills its tile.</summary>
    public Vector4F Drawn;

    /// <summary>The sub-rectangle of the source one copy samples.</summary>
    public Vector4F UvRect;

    public Vector4F Tint;
}
