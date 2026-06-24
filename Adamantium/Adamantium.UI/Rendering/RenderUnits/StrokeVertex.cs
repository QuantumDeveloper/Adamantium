using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.RenderUnits;

// The GPU stroke expander (StrokeEffect.fx) writes float3 vertices: (x, y, signed perpendicular distance from the
// centerline). The distance drives the analytic-AA coverage in StrokePS (feathers both ribbon edges + round joins/caps).
[StructLayout(LayoutKind.Sequential)]
internal struct StrokeVertex
{
    [VertexInputElement("POSITION")] public Vector2F Position;
    [VertexInputElement("TEXCOORD0")] public float Distance;
}
