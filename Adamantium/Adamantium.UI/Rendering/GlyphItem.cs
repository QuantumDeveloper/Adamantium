using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the GPU glyph batch (see FontEffect.fx, pass RenderMsdfBatchInstanced): a single glyph quad in
/// NODE-LOCAL space that the vertex shader transforms to world by its transform-table slot. Packed into a BDA STORAGE
/// buffer and read in the vertex shader by SV_InstanceID (the shader's <c>GlyphData</c>); the quad comes from
/// SV_VertexID. All-<see cref="Vector4F"/> (16-byte aligned) so the SSBO stride is unambiguous, exactly like
/// <see cref="RectItem"/> - the layout here IS the SSBO record layout (not a vertex format).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GlyphItem
{
    /// <summary>Node-local bounds: x, y, w, h (world-space for a slot-0 identity bake).</summary>
    public Vector4F LocalRect;

    /// <summary>Atlas UV rect: u, v, w, h.</summary>
    public Vector4F Source;

    /// <summary>.x = transform-table slot; .y = atlas layer; .z = depth; .w = OPACITY SLOT, sent but NOT YET READ.
    /// <para>The reason it went unread is gone. A SECOND read of the transform table from this shader used to AV
    /// <c>vkCreateShadersEXT</c> in every form tried - but that was the effect sitting at the limit of what that
    /// compiler would take, and it lost two unreachable passes since (see the note in FontEffect.fx). The clip now
    /// makes exactly that second read, so this field can be read too and text can stop folding the opacity CHAIN into
    /// its colour like the odd one out. Not done here only because nothing asked for it yet.</para></summary>
    public Vector4F Params;

    /// <summary>.x = the CLIP SLOT this glyph is cut by, or -1; .yzw spare. Its own field because every number in
    /// <see cref="Params"/> is spoken for, and -1 rather than 0 because 0 is a valid slot owned by somebody else.
    /// <para>Carrying the SLOT, like every other family, is only possible since the effect lost its two dead passes: the
    /// glyph vertex shader already reads the transform table for its matrix, and a SECOND read from it AVd
    /// <c>vkCreateShadersEXT</c> 4 starts of 4 while they were there.</para></summary>
    public Vector4F Clip;

    /// <summary>Straight (non-premultiplied) RGBA, element/brush opacity already folded into .w.</summary>
    public Vector4F Color;
}
