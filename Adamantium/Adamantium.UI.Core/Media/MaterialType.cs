namespace Adamantium.UI.Core.Media;

/// <summary>What a <see cref="MaterialBrush"/> is made of.
/// <para>The kinds differ along two independent axes, and only one of them is visible here. The SOURCE - what gets
/// captured from behind the element - is chosen by the material; the TREATMENT - what is done with that capture - is a
/// shader pass, and two materials can share it. Acrylic and LiquidGlass take the same capture and treat it differently;
/// Acrylic and Mica share a treatment and differ in what is handed to it.</para></summary>
public enum MaterialType
{
    /// <summary>Frosted glass over what is DIRECTLY behind the element in this frame: blur, tint, grain. Follows the
    /// content beneath it - scroll something under an acrylic panel and the panel shows it moving.</summary>
    Acrylic,

    /// <summary>A tinted backdrop taken from the WINDOW's background rather than from the content: blurred nearly flat,
    /// so scrolling underneath does not disturb it. Cheap for the same reason - there is nothing per-frame to follow.
    /// </summary>
    Mica,

    /// <summary>A LENS, not frosting: the capture is sampled with an offset that grows towards the shape's edge, so what
    /// is behind bends the way it does through a thick drop of glass, with a chromatic fringe and a bright rim.
    /// Refraction at zero degrades gracefully into plain acrylic - the two are ends of one range.</summary>
    LiquidGlass
}
