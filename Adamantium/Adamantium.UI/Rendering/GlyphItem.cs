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
    /// <para>The glyph VS already reads the transform table for its matrix, and this driver AVs in
    /// <c>vkCreateShadersEXT</c> on a SECOND read of it from that shader - the same compiler bug the layer-index note
    /// in FontEffect.fx describes, and it crashed the sandbox on start in every form tried (a select, and a bare
    /// multiply). So text still folds the opacity CHAIN into its colour, and this field waits: when the driver is
    /// fixed, read it in FontBatchInstancedVS and text joins the rest of the families.</para></summary>
    public Vector4F Params;

    /// <summary>Straight (non-premultiplied) RGBA, element/brush opacity already folded into .w.</summary>
    public Vector4F Color;
}
