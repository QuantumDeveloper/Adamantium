using System;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Input.Raw;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

public static class WindowsMouseDeviceExtension
{
    public static InputModifiers GetKeyModifiers(this WindowMessages msg, IntPtr wParam)
    {
        var mouseButtons = Messages.GetMouseModifyKeys(msg, wParam);
        var modifiers = KeyboardDevice.CurrentDevice.Modifiers;
        if (mouseButtons.HasFlag(MouseModifiers.LeftButton))
        {
            modifiers |= InputModifiers.LeftMouseButton;
        }
        if (mouseButtons.HasFlag(MouseModifiers.RightButton))
        {
            modifiers |= InputModifiers.RightMouseButton;
        }
        if (mouseButtons.HasFlag(MouseModifiers.MiddleButton))
        {
            modifiers |= InputModifiers.MiddleMouseButton;
        }
        if (mouseButtons.HasFlag(MouseModifiers.XButton1))
        {
            modifiers |= InputModifiers.X1MouseButton;
        }
        if (mouseButtons.HasFlag(MouseModifiers.XButton2))
        {
            modifiers |= InputModifiers.X2MouseButton;
        }
        return modifiers;
    }

    /// <summary>Translate a Win32 mouse message into the neutral event type. EXPLICITLY, every case: this used to end in
    /// <c>(RawMouseEventType)msg</c>, which only worked because the enum's values WERE the message numbers - a bargain
    /// that quietly made a platform-neutral type Windows-shaped.</summary>
    public static RawMouseEventType EventTypeFromMessage(this WindowMessages msg, IntPtr wParam)
    {
        switch (msg)
        {
            case WindowMessages.LeftButtondown:
                return RawMouseEventType.LeftButtonDown;
            case WindowMessages.LeftButtonup:
                return RawMouseEventType.LeftButtonUp;
            case WindowMessages.RightButtondown:
                return RawMouseEventType.RightButtonDown;
            case WindowMessages.RightButtonup:
                return RawMouseEventType.RightButtonUp;
            case WindowMessages.MiddleButtondown:
                return RawMouseEventType.MiddleButtonDown;
            case WindowMessages.MiddleButtonup:
                return RawMouseEventType.MiddleButtonUp;
            case WindowMessages.Xbuttondown:
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                return exactButton == MouseModifiers.XButton1 ? RawMouseEventType.X1ButtonDown : RawMouseEventType.X2ButtonDown;
            }
            case WindowMessages.Xbuttonup:
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                return exactButton == MouseModifiers.XButton1 ? RawMouseEventType.X1ButtonUp : RawMouseEventType.X2ButtonUp;
            }
            // A CS_DBLCLKS window delivers the SECOND press of a double-click as WM_*BUTTONDBLCLK (not a plain DOWN). The
            // device layer has no separate double-click event - it recomputes the click count from timing - so surface a
            // dbl-click as a normal button DOWN. Without this the second press was dropped, so ClickCount never reached 2
            // (double-click-to-maximize, "select word", etc. never fired) and double-clicking a control produced one click.
            case WindowMessages.LeftButtondblclk:
                return RawMouseEventType.LeftButtonDown;
            case WindowMessages.RightButtondblclk:
                return RawMouseEventType.RightButtonDown;
            case WindowMessages.MiddleButtondblclk:
                return RawMouseEventType.MiddleButtonDown;
            case WindowMessages.Xbuttondblclk:
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                return exactButton == MouseModifiers.XButton1 ? RawMouseEventType.X1ButtonDown : RawMouseEventType.X2ButtonDown;
            }
            case WindowMessages.Mousemove:
                return RawMouseEventType.MouseMove;
            case WindowMessages.Mouseleave:
                return RawMouseEventType.LeaveWindow;
            case WindowMessages.MouseWheel:
            case WindowMessages.MouseHwheel:
                return RawMouseEventType.MouseWheel;
            default:
                return RawMouseEventType.MouseMove;
        }
    }
}