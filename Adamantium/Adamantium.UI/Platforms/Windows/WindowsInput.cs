using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Win32 answers to the live input questions the neutral layer cannot answer itself: where the pointer is, what the
/// keyboard is doing right now, and what the user set as their double-click speed. One object for all three - each is a
/// one-line passthrough, and they are registered together as the platform comes up.
/// </summary>
internal sealed class WindowsInput : INativeMouse, INativeKeyboard, INativePlatformSettings
{
    private const int KeyPressed = 0x8000;   // the high-order bit: the key is down
    private const int KeyToggled = 0x1;      // the low-order bit: the lock is lit

    public PixelPoint Position
    {
        get
        {
            Win32Interop.GetCursorPos(out var point);
            return new PixelPoint(point.X, point.Y);
        }
        set => Win32Interop.SetCursorPos((int)value.X, (int)value.Y);
    }

    // GetAsyncKeyState, not GetKeyState: the PHYSICAL state, not the one synchronized with this thread's message queue.
    // Our input is dispatched onto the loop thread, and under a mouse capture the queue state lags badly - a drag asking
    // "is Ctrl held?" needs the real key. Key values are the Win32 virtual-key codes, so no translation is needed here.
    public bool IsKeyDown(Key key) => (Win32Interop.GetAsyncKeyState((uint)key) & KeyPressed) != 0;

    // Toggles (Caps/Num/Scroll Lock) are a latched state, which only GetKeyState reports.
    public bool IsKeyToggled(Key key) => (Win32Interop.GetKeyState((uint)key) & KeyToggled) != 0;

    public uint DoubleClickTime => Win32Interop.GetDoubleClickTime();

    // SPI_GETMOUSEHOVERTIME. The call fills an int; if it fails we say 0 and the neutral layer falls back to its default.
    public uint HoverTime
    {
        get
        {
            var value = new int[1];
            return Win32Interop.SystemParametersInfo(SPI.GetMouseHoverTime, 0, value, 0) ? (uint)value[0] : 0;
        }
    }

    // SM_CXDRAG/SM_CYDRAG - the user's own "how far before it's a drag" setting (Control Panel / registry). Read live
    // rather than cached: the user can change it while the app runs, and it is queried once per gesture, not per frame.
    public Size DragThreshold => new(
        Win32Interop.GetSystemMetrics(SystemMetrics.Cxdrag),
        Win32Interop.GetSystemMetrics(SystemMetrics.Cydrag));

    // SM_XVIRTUALSCREEN & co - every monitor as one rectangle, which starts at a negative origin when a second screen
    // sits to the left of the primary. Read live: monitors are plugged in and unplugged while an application runs, and
    // that is precisely the case this answers.
    public Rect VirtualScreen => new(
        Win32Interop.GetSystemMetrics(SystemMetrics.Xvirtualscreen),
        Win32Interop.GetSystemMetrics(SystemMetrics.Yvirtualscreen),
        Win32Interop.GetSystemMetrics(SystemMetrics.CxVirtualscreen),
        Win32Interop.GetSystemMetrics(SystemMetrics.CyVirtualscreen));
}
