using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// One instance of a shared local mesh whose FILL is a LINEAR/RADIAL gradient (the gradient sibling of
/// <see cref="GeometryInstance"/>; see <see cref="InstancedFillCollector"/>). Carries the per-element world transform +
/// the whole gradient (geometry + up to 8 stops) + the shape's LOCAL bounding box (so the pixel shader maps a fragment's
/// local mesh position to a 0..1 uv over the shape and evaluates the gradient there). Packed dense in a per-key BDA
/// storage buffer, read by the gradient-fill vertex shader by <c>SV_InstanceID</c>; the VS passes the gradient down to the
/// PS via (flat) interpolators, so the PIXEL shader never dereferences the buffer - one BDA-reading gradient PS already
/// tripped the driver's shader-object flake, and this keeps the fragment stage buffer-free.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GradientGeometryInstance
{
    /// <summary>Full per-instance world transform (element local -> world). Row-vector convention (as GeometryInstance).</summary>
    public Matrix4x4F World;

    /// <summary>.x type (1 linear/2 radial); .y spread (0 pad/1 reflect/2 repeat); .z stop count; .w interp mode (0 sRGB/1 OKLab).</summary>
    public Vector4F Params;

    /// <summary>LOCAL 0..1: linear (startXY, endXY) | radial (centerXY, radiusXY).</summary>
    public Vector4F Geom0;

    /// <summary>Radial focal (originXY, _, _); unused for linear.</summary>
    public Vector4F Geom1;

    /// <summary>The shape's local-space bounds (minX, minY, sizeX, sizeY): a fragment's uv = (localPos - min) / size.</summary>
    public Vector4F LocalBounds;

    /// <summary>Straight stop colours (opacity folded into .w); only the first Params.z are valid.</summary>
    public Vector4F Stop0, Stop1, Stop2, Stop3, Stop4, Stop5, Stop6, Stop7;

    /// <summary>Stop offsets 0..3.</summary>
    public Vector4F Offsets0;

    /// <summary>Stop offsets 4..7.</summary>
    public Vector4F Offsets1;
}
