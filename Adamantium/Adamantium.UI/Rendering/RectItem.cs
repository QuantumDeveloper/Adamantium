using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the item-background batch (see BatchEffect.fx, pass Rect): a solid rounded-rect fill with its position
/// already baked to WORLD space. Packed into a BDA STORAGE buffer and read in the vertex shader by SV_InstanceID (the
/// shader's <c>RectData</c>); the quad comes from SV_VertexID and the pixel shader reconstructs the rounded corners
/// analytically (self-anti-aliasing). No per-instance vertex buffer - the layout here is the SSBO record layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RectItem
{
    /// <summary>World-space bounds: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>.x = the LARGEST corner radius (what the quad has to make room for); .y = transform-table slot;
    /// .z = 1 when the fill must not be anti-aliased; .w reserved.</summary>
    public Vector4F Params;

    /// <summary>The four corner radii, in the CPU <c>CornerRadius</c> order: x = top-left, y = top-right,
    /// z = bottom-right, w = bottom-left. Each is independent - the shader picks the one belonging to the fragment's
    /// own corner.</summary>
    public Vector4F Radii;

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

    /// <summary>The BORDER's thickness on each side, in device px: x = left, y = top, z = right, w = bottom. All zero =
    /// no border, and the shader takes the plain fill path. A border is drawn INSIDE the bounds, in
    /// <see cref="StrokeColor"/>, together with the fill in one pass - so their shared outline is anti-aliased once.
    /// <para>Why not the pen: a pen is ONE width offset from a contour. Four widths are not an offset of anything, and a
    /// border with different sides is what every second control in a theme asks for.</para></summary>
    public Vector4F Inset;
}
