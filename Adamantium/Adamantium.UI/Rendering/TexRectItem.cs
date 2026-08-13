using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the TEXTURED rounded-rect batch (see BatchEffect.fx, pass TexRect): a rounded rect whose fill is
/// SAMPLED from a texture, position baked to WORLD space. Packed into a BDA STORAGE buffer and read by SV_InstanceID
/// (the shader's <c>TexRectData</c>); the quad comes from SV_VertexID and the pixel shader reconstructs the rounded
/// corners analytically (self-AA).
/// <para>The sibling of <see cref="PatternRectItem"/>: same SDF shape, but the fill comes from a sample instead of a
/// formula. WHICH texture is not in the record - one texture is bound per SEGMENT (see
/// <see cref="TexRectCollector"/>), the way the text batch binds one atlas per segment.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TexRectItem
{
    /// <summary>World-space bounds: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>.x = corner radius (device px; NEGATIVE = draw the ellipse SDF instead); .y = transform-table slot;
    /// .z = clip flag (1 = ONE copy that must not spill outside <see cref="Drawn"/>); .w = reserved.</summary>
    public Vector4F Params;

    /// <summary>The rectangle the picture is DRAWN in, inside <see cref="Bounds"/>: offset x, y and scale w, h, in 0..1
    /// of the bounds. A field of its own because the SHAPE must not shrink with the picture - baked as the bounds, a
    /// Uniform fill turned a circle into an oval.</summary>
    public Vector4F Drawn;

    /// <summary>The sub-rectangle of the source to sample, normalised: x, y, w, h. A whole image is (0,0,1,1); one
    /// slice of a nine-slice is its own ninth.</summary>
    public Vector4F UvRect;

    /// <summary>How many times <see cref="UvRect"/> repeats across the bounds, per axis: (1,1) stretches it once (the
    /// ordinary case), (n,1) tiles it n times horizontally. Fractional values are allowed - a tiled edge rarely divides
    /// evenly, and cutting the last tile short is what CSS <c>border-image</c> calls "round" vs "stretch".
    /// <para>.z, .w = reserved.</para></summary>
    public Vector4F UvRepeat;

    /// <summary>Multiplied into the sampled colour, straight RGBA, opacity folded into .w. White = the image as it is;
    /// a colour tints it, which is how one greyscale skin serves several themes.</summary>
    public Vector4F Tint;
}
