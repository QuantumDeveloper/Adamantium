using System;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Watches a native window for monitor-DPI changes and raises <see cref="DpiChanged"/> with the new DPI. The OS sends
/// <c>WM_DPICHANGED</c> to a per-monitor-DPI-aware top-level window the instant it crosses onto a monitor of a different DPI,
/// so a listener re-scales exactly on the crossing - no per-frame polling, and no lag from a cached window DPI. Reusable for
/// any <see cref="Win32NativeWindowWrapper"/>; <see cref="Dispose"/> detaches the hook.
/// </summary>
public sealed class WindowDpiWatcher : IDisposable
{
    private const uint WmDpiChanged = 0x02E0;

    private readonly Win32NativeWindowWrapper _window;
    private readonly WndProcHook _hook;

    /// <summary>Raised with the new DPI (dpiX; dpiX == dpiY for WM_DPICHANGED) when the window moves to a different-DPI monitor.</summary>
    public event Action<uint> DpiChanged;

    public WindowDpiWatcher(Win32NativeWindowWrapper window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _hook = OnMessage;
        _window.AddHook(_hook);
    }

    private IntPtr OnMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Don't set handled: DefWindowProc's WM_DPICHANGED handling is benign for our fixed-bitmap window, and the listener
        // does the actual re-scale + reposition itself.
        if (msg == WmDpiChanged)
        {
            uint dpi = (uint)((wParam.ToInt64() >> 16) & 0xFFFF);   // HIWORD(wParam) = new DPI
            DpiChanged?.Invoke(dpi);
        }
        return IntPtr.Zero;
    }

    public void Dispose() => _window?.RemoveHook(_hook);
}
