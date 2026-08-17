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
    /// <summary>Node-local bounding box: x, y, w, h (world for slot-0 legacy bakes - the identity matrix).</summary>
    public Vector4F Bounds;

    /// <summary>.x = transform-table slot (0 = identity); .yzw reserved. Mirrors the shader's EllipseData.Params.</summary>
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

    /// <summary>Dash runs 2..5 in device px - runs 0 and 1 ride in <see cref="Stroke0"/>.zw and the RUN COUNT is
    /// packed into <see cref="Stroke1"/>.w. A pattern longer than one ON/GAP period lives here.</summary>
    public Vector4F Dash;

    /// <summary>The angular CUT: x = start, y = end, both in RADIANS of the ellipse's own parametric angle (the one the
    /// tessellator sweeps: x = rx·cos t, y = ry·sin t - which is NOT the geometric angle unless rx == ry); z = how the
    /// cut closes (0 = no cut, a whole ellipse; 1 = <c>Sector</c>, through the centre; 2 = <c>EdgeToEdge</c>, by the
    /// chord); w = RING thickness in device px, measured inward from the outline (0 = solid).
    /// <para>A sector and a segment are the same ellipse with a STRAIGHT boundary added, so they need no shape of their
    /// own: the field is intersected with that boundary and the stroke follows the combined outline for free. A RING is
    /// the same trick inward - the field minus its own offset - which turns a ring gauge from a thick stroke into a
    /// shape, thickness and all, and hands the pen back for a real outline.</para></summary>
    public Vector4F Arc;
}
