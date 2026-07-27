using Adamantium.Mathematics;
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

    public Vector2 Position
    {
        get
        {
            Win32Interop.GetCursorPos(out var point);
            return point;
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

    // SM_CXDRAG/SM_CYDRAG - the user's own "how far before it's a drag" setting (Control Panel / registry). Read live
    // rather than cached: the user can change it while the app runs, and it is queried once per gesture, not per frame.
    public Size DragThreshold => new(
        Win32Interop.GetSystemMetrics(SystemMetrics.Cxdrag),
        Win32Interop.GetSystemMetrics(SystemMetrics.Cydrag));
}
