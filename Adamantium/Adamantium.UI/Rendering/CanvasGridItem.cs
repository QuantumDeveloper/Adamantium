using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One canvas grid (see GridEffect.fx, pass Grid): a rectangle whose every pixel decides for itself, from the world
/// coordinate under it, whether it is on a mark. Packed into a BDA storage buffer and read by SV_InstanceID; the quad
/// comes from SV_VertexID.
/// <para>There is exactly ONE of these in a draw, which is the whole point of the pass - so the colours are kept as
/// float4 rather than packed into bytes: packing would save twenty-four bytes once and cost the question of how two
/// four-byte colours align against the fields after them.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CanvasGridItem
{
    /// <summary>Node-local bounds: x, y, w, h.</summary>
    public Vector4F Bounds;

    /// <summary>.x transform-table slot; .y marks (1 dots, 2 lines); .z opacity slot, or -1; .w mark size in logical px.</summary>
    public Vector4F Params;

    /// <summary>.xy where the world's ORIGIN sits on screen, in the element's own logical pixels; .z screen pixels per
    /// world unit; .w spare.</summary>
    public Vector4F Camera;

    /// <summary>.x the step to draw (world units, ALREADY coarsened); .y the coarsening the accent level is above it;
    /// .z 1 / the pitch a mark must keep on screen, as a RECIPROCAL - the shader must not divide; .w spare.</summary>
    public Vector4F Step;

    /// <summary>.x the ancestor's rounded-clip slot, or -1; .yzw spare.</summary>
    public Vector4F Clip;

    /// <summary>The GROUND, straight RGBA. Carried by the grid because the grid is flushed first of its clip group -
    /// that is what makes it a ground - and a background drawn separately would then land on top of it.</summary>
    public Vector4F Background;

    /// <summary>Straight RGBA, opacity folded into the alpha.</summary>
    public Vector4F GridColor;

    /// <summary>Straight RGBA; alpha 0 leaves the world's axes undrawn.</summary>
    public Vector4F AxisColor;
}
