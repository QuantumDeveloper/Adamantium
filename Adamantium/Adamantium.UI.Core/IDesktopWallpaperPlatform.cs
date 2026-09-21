namespace Adamantium.UI.Core;

/// <summary>What a platform implements to answer <see cref="DesktopWallpaper"/>. One method, because the wallpaper is
/// one question: what is behind the window on this screen.</summary>
public interface IDesktopWallpaperPlatform
{
    /// <summary>The wallpaper on the monitor containing a DESKTOP point, or <see cref="WallpaperInfo.None"/> when the
    /// platform cannot tell (no monitor there, the desktop is managed by something that does not say).</summary>
    WallpaperInfo GetWallpaper(PixelPoint point);
}
