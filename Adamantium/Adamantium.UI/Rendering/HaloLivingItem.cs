using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One LIVING aura instance - the band whose reach wanders along the outline and drifts over time, travelling a palette.
/// Matches <c>HaloLivingData</c> in BatchEffect.fx field for field.
/// <para>Its own record and its own pass: a still band must not carry a palette it never reads, and the noise this one
/// evaluates is real ALU that a plain shadow has no business paying for.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct HaloLivingItem
{
    /// <summary>The SHAPE's own rect in SLOT units - the grown quad is derived in the vertex shader.</summary>
    public Vector4F Bounds;

    /// <summary>.x = corner radius; .y = transform-table slot; .z = shape (0 rect, 1 ellipse, 2 sampled field);
    /// .w = 1 for a band drawn INSIDE the outline.</summary>
    public Vector4F Params;

    /// <summary>.z = spread, .w = softness, both in SLOT units. .xy unused: an aura has no offset.</summary>
    public Vector4F Band;

    /// <summary>.x = the sampled field's range (slot units, 0 for an analytic shape); .y = turbulence; .z = flow;
    /// .w = detail.</summary>
    public Vector4F Field;

    /// <summary>The aura's own colour, used when the palette is empty.</summary>
    public Vector4F Color;

    /// <summary>.x = how many palette stops are valid; 0 = use <see cref="Color"/>.</summary>
    public Vector4F Ramp;

    public Vector4F Stop0;
    public Vector4F Stop1;
    public Vector4F Stop2;
    public Vector4F Stop3;
    public Vector4F Stop4;
    public Vector4F Stop5;
    public Vector4F Stop6;
    public Vector4F Stop7;

    public Vector4F Offsets0;
    public Vector4F Offsets1;
}
