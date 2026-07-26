using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Windows <see cref="IDragGhost"/>: a WS_EX_LAYERED top-level window whose pixels are pushed via
/// <c>UpdateLayeredWindow</c>, so the DWM composites the bitmap with per-pixel alpha - no swapchain, no Vulkan on this
/// window (that is exactly why the ghost is a static readback bitmap, not a live surface). Transparent + Topmost +
/// Noactivate + Toolwindow + click-through (WS_EX_TRANSPARENT), so it floats over everything and never steals focus or
/// eats input.
/// </summary>
public sealed class Win32DragGhost : IDragGhost
{
    private const WindowStyleEx GhostExStyle =
        WindowStyleEx.Layered | WindowStyleEx.Transparent | WindowStyleEx.Topmost |
        WindowStyleEx.Noactivate | WindowStyleEx.Toolwindow;

    private Win32NativeWindowWrapper _window;
    private WindowDpiWatcher _dpiWatcher;
    private bool _visible;

    public event Action<uint> DpiChanged;

    private void EnsureWindow(int x, int y, int width, int height)
    {
        if (_window != null) return;
        _window = new Win32NativeWindowWrapper(
            WindowClassStyle.SaveBits, GhostExStyle, WindowStyle.Popup, x, y, width, height, IntPtr.Zero);
        _dpiWatcher = new WindowDpiWatcher(_window);
        _dpiWatcher.DpiChanged += dpi => DpiChanged?.Invoke(dpi);
    }

    public void Show(byte[] premultipliedBgra, int width, int height, int screenX, int screenY)
    {
        if (premultipliedBgra == null || width <= 0 || height <= 0) return;
        EnsureWindow(screenX, screenY, width, height);

        var screenDc = Win32Interop.GetDC(IntPtr.Zero);
        var memDc = Win32Interop.CreateCompatibleDC(screenDc);

        // Top-down 32-bit DIB (negative height) so row 0 is the top - matches the readback buffer's layout.
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = Win32Interop.BI_RGB
        };

        var dib = Win32Interop.CreateDIBSection(memDc, ref header, Win32Interop.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
        var oldBmp = Win32Interop.SelectObject(memDc, dib);

        Marshal.Copy(premultipliedBgra, 0, bits, Math.Min(premultipliedBgra.Length, width * height * 4));

        var dst = new NativePoint { X = screenX, Y = screenY };
        var size = new NativeSize(width, height);
        var src = new NativePoint { X = 0, Y = 0 };
        var blend = new BLENDFUNCTION
        {
            BlendOp = Win32Interop.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = Win32Interop.AC_SRC_ALPHA
        };

        Win32Interop.UpdateLayeredWindow(_window.Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Win32Interop.ULW_ALPHA);

        Win32Interop.SelectObject(memDc, oldBmp);
        Win32Interop.DeleteObject(dib);
        Win32Interop.DeleteDC(memDc);
        Win32Interop.ReleaseDC(IntPtr.Zero, screenDc);

        if (!_visible)
        {
            Win32Interop.ShowWindow(_window.Handle, WindowShowStyle.ShowNoActivate);
            _visible = true;
        }
    }

    public void Move(int screenX, int screenY)
    {
        if (_window == null) return;
        Win32Interop.SetWindowPos(_window.Handle, IntPtr.Zero, screenX, screenY, 0, 0,
            SetWindowPosFlags.Nosize | SetWindowPosFlags.Nozorder | SetWindowPosFlags.Noactivate);
    }

    public void Hide()
    {
        if (_window == null || !_visible) return;
        Win32Interop.ShowWindow(_window.Handle, WindowShowStyle.Hide);
        _visible = false;
    }

    // DPI of the monitor the ghost is CURRENTLY on, so the drag can re-scale the bitmap when it crosses monitors. Read from
    // the MONITOR under the window (MonitorFromWindow + GetDpiForMonitor), NOT GetDpiForWindow - the latter reflects a cached
    // window DPI that only updates on WM_DPICHANGED, lagging a frame or two behind the actual crossing. 96 until shown.
    private const uint MonitorDefaultToNearest = 2;
    public uint Dpi
    {
        get
        {
            if (_window == null) return 96u;
            var monitor = Win32Interop.MonitorFromWindow(_window.Handle, MonitorDefaultToNearest);
            return Win32Interop.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? dpiX : 96u;
        }
    }

    public void Dispose()
    {
        _dpiWatcher?.Dispose();
        _dpiWatcher = null;
        _window?.Dispose();
        _window = null;
        _visible = false;
    }
}
