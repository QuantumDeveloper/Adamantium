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

    /// <summary>A tinted backdrop taken from the DESKTOP WALLPAPER behind the window, not from our own frame: blurred
    /// past recognition, so only broad colour survives. It does not react to the content at all - scrolling underneath
    /// leaves it still - and changes only when the window moves across the desktop or the wallpaper itself does.
    ///
    /// <para>Cheap for that same reason: the wallpaper is a FILE, decoded and blurred once (see
    /// <c>WallpaperBackdrop</c>), where acrylic pays a copy of the frame every time it draws. That is why mica can sit
    /// behind a whole window and acrylic is kept for panes.</para>
    ///
    /// <para>Where no wallpaper can be read - a plain-colour desktop, or a platform that does not expose one - it falls
    /// back to tinting that colour, which is a visible fallback rather than a silently disabled material.</para></summary>
    Mica,

    /// <summary>A LENS, not frosting: the capture is sampled with an offset that grows towards the shape's edge, so what
    /// is behind bends the way it does through a thick drop of glass, with a chromatic fringe and a bright rim.
    /// Refraction at zero degrades gracefully into plain acrylic - the two are ends of one range.</summary>
    LiquidGlass
}
