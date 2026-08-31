namespace Adamantium.UI.Core.Media;

/// <summary>What a <see cref="MaterialBrush.Source"/> picture is PINNED to. Only meaningful on Mica - the other two
/// always read what is directly beneath them and have nothing to pin.</summary>
public enum MaterialAnchor
{
    /// <summary>The virtual screen, with the WINDOW moving across it - how the system wallpaper behaves, hence the
    /// default. The one anchor needing the platform to say where the window is; where it cannot (Wayland gives no
    /// global coordinates) this degrades to <see cref="Window"/> rather than failing.</summary>
    Desktop,

    /// <summary>The window: the picture travels with it, so panes at different places show different parts of it while
    /// scrolling underneath changes nothing.</summary>
    Window,

    /// <summary>The element: the picture fills the shape and travels - and turns - with it. For a picture put on a
    /// CONTROL and shown through the panes on top, each revealing its own part. Still mica, not an image brush with a
    /// blur: what scrolls beneath the panes does not reach them.</summary>
    Element
}
