using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.RenderUnits;

// The GPU fill-fringe expander (FillFringeEffect.fx) writes, per vertex, a float2 position + a float coverage; this is
// that buffer's layout for the analytic-AA draw. Coverage 1 = on the fill contour (meets the solid body), 0 = outer
// fringe edge -> a ~1px feathered edge. Matches the shader's float3 output (x, y, coverage = 12 bytes).
[StructLayout(LayoutKind.Sequential)]
internal struct FringeVertex
{
    [VertexInputElement("POSITION")] public Vector2F Position;
    [VertexInputElement("TEXCOORD0")] public float Coverage;
}
