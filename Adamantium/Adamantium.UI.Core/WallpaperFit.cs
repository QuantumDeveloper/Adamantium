namespace Adamantium.UI.Core;

/// <summary>How the OS lays the wallpaper image over a monitor. Not decoration: a material that samples the wallpaper
/// has to place it EXACTLY as the desktop does, or the pane shows a different part of the picture than the one it is
/// sitting on - which is the one thing that gives the illusion away.</summary>
public enum WallpaperFit
{
    /// <summary>Centred at its own size, background colour around it.</summary>
    Center,

    /// <summary>Repeated from the top-left.</summary>
    Tile,

    /// <summary>Stretched to the monitor, aspect ratio ignored.</summary>
    Stretch,

    /// <summary>Scaled until it fits inside the monitor - letterboxed, whole picture visible.</summary>
    Fit,

    /// <summary>Scaled until it covers the monitor - cropped, no bars. The Windows 11 default.</summary>
    Fill,

    /// <summary>One picture spread across EVERY monitor rather than repeated per monitor.</summary>
    Span
}
