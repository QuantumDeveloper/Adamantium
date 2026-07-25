using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the pattern rounded-rect batch (see BatchEffect.fx, pass Pattern): a rounded rect whose fill is a
/// PROCEDURAL two-colour pattern (checkerboard/stripes/dots/grid) evaluated per fragment, position baked to WORLD space.
/// Packed into a BDA STORAGE buffer and read by SV_InstanceID (the shader's <c>PatternRectData</c>); the quad comes from
/// SV_VertexID and the pixel shader reconstructs the rounded corners analytically (self-AA) AND the pattern. Mirrors
/// <see cref="GradientRectItem"/>'s stroke fields so the shared CompositeFillStroke draws the stroke identically.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PatternRectItem
{
    /// <summary>World-space bounds: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>.x = corner radius; .y = pattern type (0 checker, 1 stripes, 2 dots, 3 grid, 4 FBM noise); .z = cell size
    /// (device px; the base noise cell for type 4); .w = transform-table slot.</summary>
    public Vector4F Params;

    /// <summary>Primary (background) colour, straight RGBA, opacity folded into .w.</summary>
    public Vector4F Color1;

    /// <summary>Secondary (feature) colour, straight RGBA, opacity folded into .w.</summary>
    public Vector4F Color2;

    /// <summary>Straight stroke colour (opacity folded into .w); .w == 0 = no stroke.</summary>
    public Vector4F StrokeColor;

    /// <summary>Stroke geometry: x = width px, y = align (-1/0/+1), z = dash ON, w = dash GAP.</summary>
    public Vector4F Stroke0;

    /// <summary>Stroke arc-length: x = dash offset, y = trim start, z = trim end, w = flags (join/cap codes).</summary>
    public Vector4F Stroke1;

    /// <summary>FBM noise params (pattern type 4 only; zero otherwise): x = octaves, y = seed, z = lacunarity, w = gain.</summary>
    public Vector4F Noise;

    /// <summary>Optional MID colour for a 3-colour gradient-map ramp of the noise (Color1 -> Color3 -> Color2); straight
    /// RGBA, opacity folded. .w == 0 = disabled (plain two-colour duotone).</summary>
    public Vector4F Color3;
}
