using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the ellipse SDF batch (see BatchEffect.fx, pass Ellipse): a solid ellipse/circle fill with its
/// bounding box baked to WORLD space. Packed into a BDA STORAGE buffer and read in the vertex shader by SV_InstanceID
/// (the shader's <c>EllipseData</c>); the quad comes from SV_VertexID and the pixel shader evaluates the ellipse implicit
/// (length(local/half) - 1), self-anti-aliasing via fwidth - resolution-independent, no tessellation, no AA fringe. No
/// per-instance vertex buffer - the layout here is the SSBO record layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct EllipseItem
{
    /// <summary>World-space bounding box: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>Straight (non-premultiplied) fill colour, element/brush opacity already folded into .w.</summary>
    public Vector4F Color;

    /// <summary>Straight stroke colour (opacity folded into .w); .w == 0 = no stroke.</summary>
    public Vector4F StrokeColor;

    /// <summary>Stroke geometry: x = width in device px, y = alignment (-1 inside, 0 center, +1 outside),
    /// z = dash ON length (device px, 0 = solid), w = dash GAP length (device px).</summary>
    public Vector4F Stroke0;

    /// <summary>Stroke arc-length features: x = dash offset (device px), y = trim start (0..1),
    /// z = trim end (0..1), w = flags (join/cap codes, packed).</summary>
    public Vector4F Stroke1;
}
