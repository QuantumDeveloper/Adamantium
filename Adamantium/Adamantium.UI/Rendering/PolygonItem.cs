using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the REGULAR POLYGON batch (see BatchEffect.fx, pass Polygon): a shape inscribed in its box whose only
/// distinguishing number is how many corners it has - 3 is a triangle, and enough of them is a circle. Packed into a BDA
/// STORAGE buffer and read by SV_InstanceID (the shader's <c>PolygonData</c>); the quad comes from SV_VertexID and the
/// pixel shader reconstructs the polygon from its signed distance, so it is self-anti-aliasing at any DPI and needs no
/// tessellation.
/// <para>Its own record rather than a flag on <see cref="EllipseItem"/>: they share a SHAPE OF RECORD, not a shape.
/// Folding one into the other would leave every ellipse carrying a corner count it never uses, and every polygon carrying
/// an angular cut it does not have.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PolygonItem
{
    /// <summary>Node-local bounding box: x, y, w, h (world for slot-0 bakes - the identity matrix).</summary>
    public Vector4F Bounds;

    /// <summary>.x = transform-table slot (0 = identity); .y = CORNERS (3 and up); .z = ring thickness in device px
    /// (0 = solid); .w = start angle in RADIANS - where corner 0 sits, 0 being the +x axis.</summary>
    public Vector4F Params;

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

    /// <summary>Dash runs 2..5 in device px - runs 0 and 1 ride in <see cref="Stroke0"/>.zw.</summary>
    public Vector4F Dash;
}
