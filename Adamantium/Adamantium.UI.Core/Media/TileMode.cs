namespace Adamantium.UI.Core.Media;

/// <summary>How a picture repeats across the shape it fills. WPF's <c>TileBrush.TileMode</c>, and the reason a small
/// texture can dress a large surface without being stretched into mush.</summary>
public enum TileMode
{
    /// <summary>One copy, laid out by the brush's <see cref="Stretch"/>. The default, and what an icon or a photo wants.</summary>
    None,

    /// <summary>Repeat the picture, every copy the same way up. Right for a texture whose edges already meet
    /// (a seamless one); a texture that does not tile seamlessly will show its seams.</summary>
    Tile,

    /// <summary>Repeat, mirroring every other copy HORIZONTALLY. Makes any picture tile without a vertical seam - the
    /// edge always meets its own reflection - at the cost of a visible symmetry.</summary>
    FlipX,

    /// <summary>Repeat, mirroring every other copy VERTICALLY.</summary>
    FlipY,

    /// <summary>Repeat, mirroring on both axes. The seam-free option for a texture that was never drawn to tile.</summary>
    FlipXY
}
