using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One record of ink (see InkEffect.fx, pass Segments). Two kinds share the shape: a HEADER, one per stroke, which is
/// the only one that draws - its fragments decide how far they are from the whole polyline at once - and a POINT, one
/// per point of that stroke, written straight after its header and read by the header's fragment shader.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct InkSegmentItem
{
    /// <summary>HEADER: .xy the stroke's low corner, .zw its high corner. POINT: .xy the point - all node-local.</summary>
    public Vector4F Segment;

    /// <summary>.x half width in node-local units; .y transform slot; .z opacity slot, or -1; .w 1 on a header, 0 on a
    /// point - which is how the vertex shader knows which records draw.</summary>
    public Vector4F Params;

    /// <summary>HEADER: .x the ancestor's rounded-clip slot, or -1; .y how many points; .z how far past this record the
    /// first of them is - RELATIVE, because the draw bases the buffer at the run it flushes; .w spare.</summary>
    public Vector4F Clip;

    /// <summary>Straight RGBA, opacity folded into the alpha.</summary>
    public Vector4F Color;
}
