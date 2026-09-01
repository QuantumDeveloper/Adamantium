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
    /// .z = 1 when the fill must not be anti-aliased; .w unused.</summary>
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

    /// <summary>.x = the CLIP SLOT this instance is cut by, or -1. A rounded clip cannot be a scissor, so the shape
    /// travels in a transform-table slot and the fragment's coverage is multiplied by it; .yzw spare.</summary>
    public Vector4F Clip;

    /// <summary>WHOSE instance this is - the paint group that baked it. CPU bookkeeping: no shader reads it.
    /// <para>It is here, in the instance, rather than in a table beside it, because the arena moves these bytes
    /// constantly - a re-issued layer copies its neighbours over, a patch rebuilds a whole range - and anything kept
    /// alongside would have to be moved by every one of those paths. That is a rule that gets forgotten, and forgetting
    /// it blanks LIVE content. Riding in the instance, ownership travels with the bytes by construction.</para>
    /// <para>What it is FOR: a segment is drawn as a RANGE, so the instances of a control that stopped drawing are
    /// re-issued along with the neighbours they sit between - a scrollbar the window outgrew, still painting its track
    /// at the size it had when it was last needed. Knowing whose each slot is, is what lets exactly those be blanked.</para></summary>
    public int OwnerTag;
}
