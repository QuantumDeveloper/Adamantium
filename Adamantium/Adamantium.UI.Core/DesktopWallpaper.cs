using System;

namespace Adamantium.UI.Core;

/// <summary>The desktop wallpaper, as a MATERIAL SOURCE. Mica shows the picture behind the WINDOW, not the frame behind
/// the element - so unlike acrylic it has no source inside our own render at all, and this is where it comes from.
///
/// <para>Per MONITOR, because the desktop is: each screen can carry its own picture and its own fit, and a window is
/// only ever on one of them at a time (spanned wallpaper is the exception, and <see cref="WallpaperFit.Span"/> is how a
/// platform says so). Callers pass a point on the desktop and get the answer for the screen under it.</para>
///
/// <para>NOT a live capture, and that is the whole economy of it: the picture is a file, so it is loaded once, blurred
/// once, and then only re-read when the OS says it changed. Mica costs a texture, where acrylic costs a copy per frame.</para>
///
/// <para>A platform that does not answer is not a failure state to hide - Linux desktops keep the wallpaper in each
/// environment's own config and some do not expose it at all. <see cref="Current"/> then returns
/// <see cref="WallpaperInfo.None"/>, the material falls back to its tint over the window's own background, and that
/// fallback is visible rather than silent.</para></summary>
public static class DesktopWallpaper
{
    /// <summary>The platform that answers, registered once at startup. Null on a platform with no implementation yet.</summary>
    public static IDesktopWallpaperPlatform Platform { get; set; }

    /// <summary>What the desktop shows on the monitor under <paramref name="point"/> (a DESKTOP point, physical - see
    /// <see cref="PixelPoint"/>), or <see cref="WallpaperInfo.None"/> when nothing can say.</summary>
    public static WallpaperInfo Current(PixelPoint point)
        => Platform?.GetWallpaper(point) ?? WallpaperInfo.None;

    /// <summary>Raised when the user changes the wallpaper, the theme, or the slideshow turns the page. What listens is
    /// whatever holds the blurred copy: the picture is cached precisely because it does not change, so the one moment
    /// it does has to be heard.
    ///
    /// <para>The announcement is the FAST path, not the only one. It is sent by whoever changes the wallpaper, and not
    /// every mechanism sends it - so a holder of the picture also re-asks <see cref="Current"/> at moments it is doing
    /// work anyway (the window moving to another monitor, coming back to the foreground) and compares the answer. Two
    /// <see cref="WallpaperInfo"/> values compare by content, which is the whole point of it being a record.</para></summary>
    public static event Action Changed;

    /// <summary>Called BY a platform when the OS reports a change. Public because the platform layer is a separate
    /// assembly - the same reason <see cref="Platform"/> is settable from outside.</summary>
    public static void RaiseChanged() => Changed?.Invoke();
}
