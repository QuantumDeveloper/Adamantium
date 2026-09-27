using System;
using System.Runtime.InteropServices;
using Adamantium.Win32;

namespace Adamantium.Win32.Shell;

/// <summary>
/// The shell's wallpaper service (<c>CLSID_DesktopWallpaper</c>, Windows 8 and later).
///
/// <para>Used instead of <c>SystemParametersInfo(SPI_GETDESKWALLPAPER)</c> because that one predates multiple monitors:
/// it returns a single path for the whole desktop, and says nothing about how the picture is laid out. A material that
/// samples the wallpaper needs both - a window on the second screen must show THAT screen's picture, placed the way the
/// desktop places it.</para>
///
/// <para>Only the members we answer with are declared, but the ORDER of every method up to them is part of the vtable
/// and cannot be shortened - hence the unused slots below, kept as named placeholders rather than deleted.</para>
/// </summary>
[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDesktopWallpaper
{
    [PreserveSig]
    int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    /// <summary>The picture on one monitor. A null <paramref name="monitorId"/> asks for the whole desktop, which
    /// answers only when every monitor shows the same file.</summary>
    [PreserveSig]
    int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId,
        [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);

    /// <summary>The shell's id for the monitor at an index - the string every other method here takes.</summary>
    [PreserveSig]
    int GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorId);

    [PreserveSig]
    int GetMonitorDevicePathCount(out uint count);

    /// <summary>The monitor's rectangle in DESKTOP coordinates - what tells us which screen a window is on.</summary>
    [PreserveSig]
    int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out RECT displayRect);

    /// <summary>The colour behind the picture, and the whole desktop when there is no picture at all. COLORREF:
    /// 0x00BBGGRR.</summary>
    [PreserveSig]
    int SetBackgroundColor(uint color);

    [PreserveSig]
    int GetBackgroundColor(out uint color);

    [PreserveSig]
    int SetPosition(DesktopWallpaperPosition position);

    /// <summary>How the picture is laid over the monitor.</summary>
    [PreserveSig]
    int GetPosition(out DesktopWallpaperPosition position);

    // ---- declared for the vtable only; nothing below is called ----------------------------------------------------
    [PreserveSig] int SetSlideshow(IntPtr items);
    [PreserveSig] int GetSlideshow(out IntPtr items);
    [PreserveSig] int SetSlideshowOptions(int options, uint slideshowTick);
    [PreserveSig] int GetSlideshowOptions(out int options, out uint slideshowTick);
    [PreserveSig] int AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorId, int direction);
    [PreserveSig] int GetStatus(out int state);
    [PreserveSig] int Enable(bool enable);
}
