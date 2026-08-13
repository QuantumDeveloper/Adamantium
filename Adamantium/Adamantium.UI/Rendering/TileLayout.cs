using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering;

/// <summary>Everything the textured passes need to draw one <see cref="Core.Media.TileBrush"/>, in the form the shader
/// reads it. Produced by <see cref="ImageTiling"/>; a plain result rather than four out-parameters because the four
/// numbers only mean anything together.</summary>
internal readonly struct TileLayout
{
    /// <summary>The part of the SOURCE one tile samples, normalised: x, y, w, h. The viewbox, already cropped if
    /// <see cref="Core.Media.Stretch.UniformToFill"/> asked for it.</summary>
    public readonly Vector4F UvRect;

    /// <summary>The tile GRID over the filled shape: how many tiles fit per axis (.xy) and where the grid starts, in
    /// tiles (.zw). A shape coordinate t (0..1) is in tile space at <c>t * xy - zw</c>.</summary>
    public readonly Vector4F Tile;

    /// <summary>The rectangle the content occupies INSIDE one tile: offset x, y and scale w, h, in 0..1 of the tile.
    /// Whole tile for Fill; letterboxed and aligned for Uniform / None. Per TILE, not per shape - a tiled Uniform brush
    /// leaves a gap around EVERY copy, not one around the lot.</summary>
    public readonly Vector4F Drawn;

    /// <summary>Mirror flags: 1 = X, 2 = Y, 3 = both. Packed as a number the shader reads branch-free.</summary>
    public readonly float Mirror;

    /// <summary>Whether the tile REPEATS. A single copy must not wrap: past its edge there is nothing, not the
    /// picture's opposite side.</summary>
    public readonly bool Repeats;

    public TileLayout(Vector4F uvRect, Vector4F tile, Vector4F drawn, float mirror, bool repeats)
    {
        UvRect = uvRect;
        Tile = tile;
        Drawn = drawn;
        Mirror = mirror;
        Repeats = repeats;
    }
}
