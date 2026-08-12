using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One soft band drawn UNDER a shape - an aura or a shadow, which differ only in what the author called them and where
/// the offset points. Matches <c>HaloRectData</c> in BatchEffect.fx field for field.
/// <para>The shape is reconstructed from an SDF exactly as the fill batches do, so the band costs no offscreen target
/// and no blur pass: it is the same signed distance, read further out and shaped by a falloff.</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct HaloRectItem
{
    /// <summary>The SHAPE's own rect in SLOT units (x, y, w, h) - not the grown quad, which the vertex shader derives.</summary>
    public Vector4F Bounds;

    /// <summary>.x = corner radius; .y = transform-table slot; .z = shape (0 rounded rect, 1 ellipse); .w = 1 for a band
    /// drawn INSIDE the outline.</summary>
    public Vector4F Params;

    /// <summary>.xy = offset, .z = spread (how far full strength reaches past the outline), .w = softness (how far it
    /// fades over). All in SLOT units; the vertex shader converts to device pixels the way every SDF batch does.</summary>
    public Vector4F Band;

    /// <summary>Straight-alpha RGBA, the author's Opacity already folded into .w.</summary>
    public Vector4F Color;

    /// <summary>.x = the distance range the sampled field encodes, in SLOT units (0 for an analytic shape). The shader
    /// needs it to turn a texel back into a distance.</summary>
    public Vector4F Field;
}
