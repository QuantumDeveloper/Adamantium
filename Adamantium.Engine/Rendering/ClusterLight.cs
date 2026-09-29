using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct ClusterLight
{
    public const uint Point = 0;
    public const uint Spot = 1;
    public const uint Directional = 2;

    public Vector4F PositionRange;
    public Vector4F ColorCosOuter;
    public Vector4F DirectionCosInner;
    public uint Kind;
    public uint Reserved0;
    public uint Reserved1;
    public uint Reserved2;
}
