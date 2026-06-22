using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering.RenderUnits;

// The GPU stroke expander (StrokeExpand.fx) writes float2 positions; this is that buffer's vertex layout for the draw.
[StructLayout(LayoutKind.Sequential)]
internal struct StrokeVertex
{
    [VertexInputElement("POSITION")] public Vector2F Position;
}
