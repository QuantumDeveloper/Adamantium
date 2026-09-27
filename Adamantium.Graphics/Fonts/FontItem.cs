using System;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>
/// Describes one glyph batch item. Bound as per-instance data ([PerInstanceData]): one item = one glyph quad,
/// expanded in the vertex shader from SV_VertexID (no geometry shader).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[PerInstanceData]
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

    /// <summary>
    /// Atlas array LAYER: which Texture2DArray slice this glyph's UVs live in. The vertex shader passes it as UV.z so
    /// the pixel shader samples the right slice. Kept as float for a portable vertex attribute.
    /// </summary>
    [VertexInputElement("PSIZE2")]
    public Single Layer;
}