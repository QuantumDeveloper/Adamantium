using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics;

/// <summary>
/// Describes one sprite batch item
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SpriteBatchItem
{
    /// <summary>
    /// Sprite destination rectangle where sprite item will be placed
    /// </summary>
    [VertexInputElement("SV_Position")]
    public Vector4F Destination;

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
    public Color Color;

    /// <summary>
    /// Texture width, height and ID (-1, 0)
    /// </summary>
    [VertexInputElement("BINORMAL0")]
    public Int4 TextureInfo;

    /// <summary>
    /// Sprite effects
    /// </summary>
    [VertexInputElement("BLENDINDICES0")]
    public int SpriteEffects;
}