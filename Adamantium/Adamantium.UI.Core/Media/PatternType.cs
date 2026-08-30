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
    Grid = 3,

    /// <summary>Thin Color2 honeycomb (hexagonal) grid lines over a Color1 background.</summary>
    Hexagon = 4,

    /// <summary>Thin Color2 diagonal (45 deg) hatch lines over a Color1 background.</summary>
    Hatch = 5,

    /// <summary>Over-under WEAVE - the carbon-fibre look: two ribbons crossing in each cell, which one lies on top
    /// alternating like a checkerboard, with the shading across each ribbon that makes the crossing read as depth.
    /// A MATERIAL by eye, a PATTERN by nature: it draws itself and needs nothing of what is behind it, which is what
    /// keeps it here instead of in MaterialBrush.</summary>
    Weave = 6
}
