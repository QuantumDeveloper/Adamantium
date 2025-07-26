using System;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Input.Raw;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;

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

    public static InputModifiers GetRawMouseModifiers(this RawMouse rawMouse)
    {
        InputModifiers modifiers = InputModifiers.None;

        var buttons = rawMouse.Data.ButtonFlags;
        if (buttons.HasFlag(RawMouseButtons.LeftDown))
        {
            modifiers |= InputModifiers.LeftMouseButton;
        }
        if (buttons.HasFlag(RawMouseButtons.MiddleDown))
        {
            modifiers |= InputModifiers.MiddleMouseButton;
        }
        if (buttons.HasFlag(RawMouseButtons.RightDown))
        {
            modifiers |= InputModifiers.RightMouseButton;
        }
        if (buttons.HasFlag(RawMouseButtons.Button4Down))
        {
            modifiers |= InputModifiers.X1MouseButton;
        }
        if (buttons.HasFlag(RawMouseButtons.Button5Down))
        {
            modifiers |= InputModifiers.X2MouseButton;
        }
        return modifiers;
    }

    public static RawMouseEventType EventTypeFromMessage(this WindowMessages msg, IntPtr wParam)
    {
        switch (msg)
        {
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
            default:
                return (RawMouseEventType)msg;
        }
    }
}