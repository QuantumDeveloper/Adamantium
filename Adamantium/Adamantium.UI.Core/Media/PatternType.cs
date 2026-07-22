namespace Adamantium.UI.Core.Media;

/// <summary>The procedural pattern a <see cref="PatternBrush"/> tiles (evaluated per fragment in the SDF batch, so it stays
/// resolution-independent - crisp at any zoom, no tiled texture).</summary>
public enum PatternType
{
    /// <summary>Alternating squares (the classic transparency backdrop).</summary>
    Checkerboard,

    /// <summary>Vertical bands alternating the two colours.</summary>
    Stripes,

    /// <summary>A dot of Color2 centred in each cell over a Color1 background.</summary>
    Dots,

    /// <summary>Thin Color2 grid lines at each cell boundary over a Color1 background.</summary>
    Grid
}
