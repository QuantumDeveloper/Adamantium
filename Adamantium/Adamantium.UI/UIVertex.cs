using System.Runtime.InteropServices;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI;

[StructLayout(LayoutKind.Sequential)]
public struct UIVertex
{
    public UIVertex(
        Vector3F position,
        Vector3F normal,
        Color color,
        Vector2F uv0)
    {
        Position = position;
        Normal = normal;
        Color = color.ToVector4();
        UV0 = uv0;
    }
        
    /// <summary>
    /// XYZ position.
    /// </summary>
    [VertexInputElement("SV_POSITION")] public Vector3F Position;

    /// <summary>
    /// The vertex color.
    /// </summary>
    [VertexInputElement("COLOR")] public Vector4F Color;

    /// <summary>
    /// The vertex normal.
    /// </summary>
    [VertexInputElement("NORMAL")] public Vector3F Normal;

    /// <summary>
    /// UV texture coordinates.
    /// </summary>
    [VertexInputElement("TEXCOORD0")] public Vector2F UV0;
}