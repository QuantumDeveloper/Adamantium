using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>
/// One ARROW (see ArrowEffect.fx, pass Run): a shaft between two points with a head on either end, or neither, all of
/// it decided per pixel. One record per arrow - there is no second kind here, and nothing to read past it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CanvasArrowItem
{
    /// <summary>.xy the start, .zw the end - node-local.</summary>
    public Vector4F Ends;

    /// <summary>.x half thickness in node-local units; .y transform slot; .z opacity slot, or -1; .w how far back from
    /// a tip a head reaches.</summary>
    public Vector4F Params;

    /// <summary>.x how far to each side of the shaft a head reaches; .y what sits on the start; .z what sits on the
    /// end - 0 nothing, 1 barbs, 2 a filled triangle; .w spare.</summary>
    public Vector4F Head;

    /// <summary>.x the ancestor's rounded-clip slot, or -1; .yzw spare.</summary>
    public Vector4F Clip;

    /// <summary>Straight RGBA, opacity folded into the alpha.</summary>
    public Vector4F Color;
}
