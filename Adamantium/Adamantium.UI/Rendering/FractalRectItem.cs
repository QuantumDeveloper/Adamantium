using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One instance of the fractal rounded-rect batch (see BatchEffect.fx, pass Fractal): a rounded rect whose fill is an
/// escape-time fractal (Julia/Mandelbrot) iterated per fragment, position baked to WORLD space. Packed into a BDA STORAGE
/// buffer, read by SV_InstanceID (the shader's <c>FractalRectData</c>); the quad comes from SV_VertexID and the pixel
/// shader reconstructs the rounded corners analytically (self-AA) AND iterates the fractal. Mirrors the pattern batch's
/// stroke fields so the shared CompositeFillStroke draws the shape edge (and any stroke) identically.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FractalRectItem
{
    /// <summary>World-space bounds: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>.x = corner radius; .y = fractal type (0 Julia, 1 Mandelbrot); .z = transform-table slot; .w = max iterations.</summary>
    public Vector4F Params;

    /// <summary>The four corner radii: x = top-left, y = top-right, z = bottom-right, w = bottom-left.</summary>
    public Vector4F Radii;

    /// <summary>.x/.y = complex-plane centre (pan); .z = zoom (magnification); .w = morph speed (auto-morph drift rate).</summary>
    public Vector4F Geom;

    /// <summary>.x/.y = Julia constant C; .z = animate flag (0/1 - auto-morph C over time); .w = reserved.</summary>
    public Vector4F Julia;

    /// <summary>Escape-ramp colour at LOW escape counts (straight RGBA, opacity folded into .w).</summary>
    public Vector4F Color1;

    /// <summary>Escape-ramp colour at HIGH escape counts (straight RGBA, opacity folded into .w).</summary>
    public Vector4F Color2;

    /// <summary>Straight stroke colour (opacity folded into .w); .w == 0 = no stroke.</summary>
    public Vector4F StrokeColor;

    /// <summary>Stroke geometry: x = width px, y = align (-1/0/+1), z = dash ON, w = dash GAP.</summary>
    public Vector4F Stroke0;

    /// <summary>Stroke arc-length: x = dash offset, y = trim start, z = trim end, w = flags (join/cap codes).</summary>
    public Vector4F Stroke1;

    /// <summary>Dash runs 2..5 in device px - runs 0 and 1 ride in <see cref="Stroke0"/>.zw and the RUN COUNT is
    /// packed into <see cref="Stroke1"/>.w. A pattern longer than one ON/GAP period lives here.</summary>
    public Vector4F Dash;

    /// <summary>Perturbation deep-zoom: .x = reference-orbit start index into the shared orbit buffer (OrbitAddress),
    /// .y = orbit length, .z = deep flag (1 = iterate the perturbation path), .w = reserved.</summary>
    public Vector4F Ref;
}
