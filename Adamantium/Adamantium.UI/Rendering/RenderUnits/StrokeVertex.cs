using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.RenderUnits;

// The GPU stroke expander (StrokeEffect.fx) writes 10 floats per vertex: position, then one float4 per END of the PIECE
// this vertex belongs to (one dash, one trim run, or the whole open contour). Cap0 = (perp, uA, vA, arcA), Cap1 =
// (caps, uB, vB, arcB): perp drives the analytic-AA band, u/v place the vertex in that end's straight frame (the cap's
// SHAPE), arc is its distance to that end along the contour (the cap's REACH), and caps packs both cap codes base-8.
// That is what lets the shader carve the caps analytically instead of emitting them as geometry.
[StructLayout(LayoutKind.Sequential)]
internal struct StrokeVertex
{
    [VertexInputElement("POSITION")] public Vector2F Position;
    [VertexInputElement("TEXCOORD0")] public Vector4F Cap0;
    [VertexInputElement("TEXCOORD1")] public Vector4F Cap1;
}
