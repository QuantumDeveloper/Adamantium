namespace Adamantium.UI.Core.Media;

/// <summary>What the four EDGES and the centre of a <see cref="NineSliceBrush"/> do with the space between the corners.
/// The corners themselves never do either - not distorting them is the whole point of a nine-slice.</summary>
public enum NineSliceEdgeMode
{
    /// <summary>Stretch the strip to fill the gap. Right for a gradient or a plain bevel; wrong for anything with a
    /// rhythm, which smears.</summary>
    Stretch,

    /// <summary>Repeat the strip along its axis, cutting the last one short. Right for a texture with a pattern in it -
    /// studs, stitching, hatching - which stretching would smear.</summary>
    Repeat,

    /// <summary>Repeat, but with the strip stretched or squeezed a little so a WHOLE number of them fits - CSS
    /// <c>border-image-repeat: round</c>. The rhythm stays even and no tile is cut mid-motif, at the cost of the strip
    /// not being drawn at exactly its own size. Usually what a studded or stitched edge actually wants.</summary>
    Round
}
