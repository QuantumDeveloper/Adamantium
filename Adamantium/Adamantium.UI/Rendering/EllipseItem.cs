using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the ellipse SDF batch (see BatchEffect.fx, technique EllipseBatch): a solid ellipse/circle fill with
/// its bounding box baked to WORLD space. Bound as per-instance data - the vertex shader expands it into a quad (corner
/// from SV_VertexID) and the pixel shader evaluates the ellipse implicit (length(local/half) - 1) and self-anti-aliases
/// via fwidth. So the shape is resolution-independent (no tessellation, crisp at any DPI/zoom) and needs no AA fringe.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[PerInstanceData]
public struct EllipseItem
{
    /// <summary>World-space bounding box: x, y, w, h.</summary>
    [VertexInputElement("SV_Position")]
    public Vector4F Bounds;

    /// <summary>Straight (non-premultiplied) fill colour, element/brush opacity already folded into .w.</summary>
    [VertexInputElement("COLOR0")]
    public Vector4F Color;

    /// <summary>Straight stroke colour (opacity folded into .w); .w == 0 = no stroke.</summary>
    [VertexInputElement("COLOR1")]
    public Vector4F StrokeColor;

    /// <summary>Stroke geometry: x = width in device px, y = alignment (-1 inside, 0 center, +1 outside),
    /// z = dash ON length (device px, 0 = solid), w = dash GAP length (device px).</summary>
    [VertexInputElement("TEXCOORD0")]
    public Vector4F Stroke0;

    /// <summary>Stroke arc-length features: x = dash offset (device px), y = trim start (0..1),
    /// z = trim end (0..1), w = flags (join/cap codes, packed).</summary>
    [VertexInputElement("TEXCOORD1")]
    public Vector4F Stroke1;
}
