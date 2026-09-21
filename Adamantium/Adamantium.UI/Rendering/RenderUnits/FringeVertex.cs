using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.RenderUnits;

// The GPU fill-fringe expander (FillFringeEffect.fx) writes, per vertex, a contour position plus the two adjacent EDGE
// DIRECTIONS; this is that buffer's layout for the analytic-AA draw. Both directions zero = a vertex ON the contour
// (coverage 1, meets the solid body); non-zero = the outer edge (coverage 0), which the vertex shader pushes out one
// device pixel along the screen-space miter it builds from them. Nothing here depends on scale, so identical meshes
// produce identical rings. Matches the shader's FringeVert (6 floats = 24 bytes).
[StructLayout(LayoutKind.Sequential)]
internal struct FringeVertex
{
    [VertexInputElement("POSITION")] public Vector2F Position;
    [VertexInputElement("TEXCOORD0")] public Vector2F Dir0;
    [VertexInputElement("TEXCOORD1")] public Vector2F Dir1;
}
