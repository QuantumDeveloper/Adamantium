using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// One instance of a shared local mesh whose FILL is a PROCEDURAL pattern/noise brush (the pattern sibling of
/// <see cref="GradientGeometryInstance"/>; see <see cref="InstancedFillCollector"/>). Carries the per-element world
/// transform + the pattern fields + the shape's LOCAL bounds, so the pixel shader maps a fragment's local mesh position to
/// the shape origin and evaluates the SAME <c>PatternMix</c>/noise the SDF rect pattern pass uses - giving pattern/noise
/// brushes on ARBITRARY geometry (Path/Polygon/glyphs), not just rects. Packed dense in a per-key BDA storage buffer, read
/// by the pattern-fill shader by <c>SV_InstanceID</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PatternGeometryInstance
{
    /// <summary>Per-instance transform RELATIVE to the transform-table slot in <see cref="Params"/>.w (element local ->
    /// slot space). Row-vector convention (as GeometryInstance). The vertex shader applies the slot matrix on top, so
    /// moving the slot's node never touches this record.</summary>
    public Matrix4x4F Local;

    /// <summary>.y = pattern type (matches PatternRectData: 0 checker..4 simplex/7 perlin/8 value/9 worley/10 ridged/
    /// 11 turbulence/12 voronoi-borders/13 combustible); .z = cell size in LOCAL units; .w = transform-table slot
    /// (same place the SDF pattern keeps it). .x unused.</summary>
    public Vector4F Params;

    /// <summary>The shape's local-space bounds (minX, minY, sizeX, sizeY): the pattern origin is minXY; combustible centres on it.</summary>
    public Vector4F LocalBounds;

    /// <summary>Primary colour, straight RGBA, opacity folded.</summary>
    public Vector4F Color1;

    /// <summary>Secondary colour, straight RGBA, opacity folded.</summary>
    public Vector4F Color2;

    /// <summary>Optional MID colour for the 3-colour noise gradient-map (.w == 0 = off). Also the combustible custom ramp mid.</summary>
    public Vector4F Color3;

    /// <summary>Noise params (noise types only): x octaves (sign = animate flag), y seed, z lacunarity, w gain
    /// (or, for combustible, the fire-palette flag).</summary>
    public Vector4F Noise;
}
