using System;
using System.IO;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.Win32;
using Adamantium.Win32.Shell;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>Windows' answer to <see cref="DesktopWallpaper"/>, through the shell's wallpaper service.
///
/// <para>Per monitor, because that service is: the picture, its layout and the screen rectangle all come back keyed by
/// a monitor id, so the question "what is behind THIS window" is answered by finding the monitor whose rectangle holds
/// the point and asking about that one.</para>
///
/// <para>The COM object is created once and kept. It is cheap to hold, and re-creating it per query would put a COM
/// activation on a path a material may take whenever the wallpaper changes.</para>
/// </summary>
internal sealed class WindowsDesktopWallpaper : IDesktopWallpaperPlatform
{
    private IDesktopWallpaper _shell;
    private bool _unavailable;

    public WallpaperInfo GetWallpaper(PixelPoint point)
    {
        var shell = Shell();
        if (shell == null) return WallpaperInfo.None;

        try
        {
            if (shell.GetMonitorDevicePathCount(out var count) != 0 || count == 0) return WallpaperInfo.None;

            for (uint i = 0; i < count; i++)
            {
                if (shell.GetMonitorDevicePathAt(i, out var monitorId) != 0 || string.IsNullOrEmpty(monitorId)) continue;
                if (shell.GetMonitorRECT(monitorId, out var rect) != 0) continue;
                if (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom) continue;

                return Describe(shell, monitorId, rect);
            }
        }
        catch (Exception)
        {
            // A shell that throws is a shell that cannot answer - the material's fallback is the same as on a desktop
            // with no wallpaper service at all, so there is nothing else to do with it here.
            _unavailable = true;
            _shell = null;
        }

        return WallpaperInfo.None;
    }

    private WallpaperInfo Describe(IDesktopWallpaper shell, string monitorId, RECT rect)
    {
        var bounds = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

        var fit = shell.GetPosition(out var position) == 0 ? Convert(position) : WallpaperFit.Fill;
        var background = shell.GetBackgroundColor(out var colorRef) == 0 ? FromColorRef(colorRef) : Colors.Black;

        // A path that is empty, or names a file that is no longer there, means "no picture" - a desktop painted with
        // the background colour, which the slideshow leaves behind between pictures too. That is an ANSWER: the monitor
        // is known, so a material tints the colour instead of falling back to its own background.
        var known = shell.GetWallpaper(monitorId, out var path) == 0 && !string.IsNullOrEmpty(path) && File.Exists(path);

        // The file's write time rides along as the revision. Windows Spotlight turns the page by REWRITING its cache
        // file, keeping the path identical - so a change watched by path alone is a change never seen.
        var revision = known ? File.GetLastWriteTimeUtc(path) : default;

        return new WallpaperInfo(known ? new Uri(path) : null, fit, background, bounds, revision);
    }

    private IDesktopWallpaper Shell()
    {
        if (_shell != null || _unavailable) return _shell;

        try
        {
            var clsid = Win32Interop.ClsidDesktopWallpaper;
            var iid = typeof(IDesktopWallpaper).GUID;
            if (Win32Interop.CoCreateInstance(ref clsid, IntPtr.Zero, Win32Interop.ClsCtxAll, ref iid,
                    out var instance) == 0)
            {
                _shell = instance as IDesktopWallpaper;
            }
        }
        catch (Exception)
        {
            _shell = null;
        }

        // Asked ONCE. The service arrived in Windows 8, so an older system will never grow one, and a material that
        // samples the wallpaper would otherwise attempt an activation on every wallpaper change forever.
        _unavailable = _shell == null;
        return _shell;
    }

    private static WallpaperFit Convert(DesktopWallpaperPosition position) => position switch
    {
        DesktopWallpaperPosition.Center => WallpaperFit.Center,
        DesktopWallpaperPosition.Tile => WallpaperFit.Tile,
        DesktopWallpaperPosition.Stretch => WallpaperFit.Stretch,
        DesktopWallpaperPosition.Fit => WallpaperFit.Fit,
        DesktopWallpaperPosition.Span => WallpaperFit.Span,
        _ => WallpaperFit.Fill
    };

    /// <summary>COLORREF is 0x00BBGGRR - blue and red the other way round from everything else here.</summary>
    private static Color FromColorRef(uint colorRef)
        => Color.FromRgba((byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF), 255);
}
