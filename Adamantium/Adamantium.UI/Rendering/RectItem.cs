using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the item-background batch (see RectBatchEffect.fx): a solid rounded-rect fill with its position
/// already baked to WORLD space. Bound as per-instance data - the vertex shader expands it into a quad (corner from
/// SV_VertexID) and the pixel shader reconstructs the rounded corners analytically (self-anti-aliasing).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[PerInstanceData]
public struct RectItem
{
    /// <summary>World-space bounds: x, y, w, h.</summary>
    [VertexInputElement("SV_Position")]
    public Vector4F Bounds;

    /// <summary>.x = corner radius (uniform); .yzw reserved.</summary>
    [VertexInputElement("TEXCOORD0")]
    public Vector4F Params;

    /// <summary>Straight (non-premultiplied) fill colour, element/brush opacity already folded into .w.</summary>
    [VertexInputElement("COLOR0")]
    public Vector4F Color;
}
