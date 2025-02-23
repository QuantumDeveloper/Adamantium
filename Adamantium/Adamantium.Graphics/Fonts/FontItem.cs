using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>
/// Describes one sprite batch item
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FontItem
{
    /// <summary>
    /// Sprite destination rectangle where sprite item will be placed
    /// </summary>
    [VertexInputElement("SV_Position")]
    public Vector4F ArrangeRect;

    /// <summary>
    /// Sprite source rectangle which texture data for rendering will be taken from
    /// </summary>
    [VertexInputElement("TEXCOORD0")]
    public Vector4F Source;

    /// <summary>
    /// Sprite origin relative to left top window corner
    /// </summary>
    [VertexInputElement("TEXCOORD1")]
    public Vector2F Origin;

    /// <summary>
    /// Sprite depth
    /// </summary>
    [VertexInputElement("PSIZE0")]
    public Single Depth;

    /// <summary>
    /// Sprite rotation
    /// </summary>
    [VertexInputElement("PSIZE1")]
    public Single Rotation;

    /// <summary>
    /// Sprite color
    /// </summary>
    [VertexInputElement("COLOR0")]
    public Vector4F Color;

    /// <summary>
    /// Sprite effects
    /// </summary>
    [VertexInputElement("BLENDINDICES0")]
    public int SpriteEffects;
}