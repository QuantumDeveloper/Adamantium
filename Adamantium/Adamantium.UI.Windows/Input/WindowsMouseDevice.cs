using Adamantium.UI.Input.Raw;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;
using System;

namespace Adamantium.UI.Input
{
    public class WindowsMouseDevice : MouseDevice
    {
        public static MouseDevice Instance { get; } = new WindowsMouseDevice();

        private WindowsMouseDevice()
        {
        }

        internal static InputModifiers GetKeyModifiers(WindowMessages msg, IntPtr wParam)
        {
            var mouseButtons = Messages.GetMouseModifyKeys(msg, wParam);
            var modifiers = WindowsKeyboardDevice.Instance.Modifiers;
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

        internal static InputModifiers GetRawMouseModifiers(RawMouse rawMouse)
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

        internal static RawMouseEventType EventTypeFromMessage(WindowMessages msg, IntPtr wParam)
        {
            if (msg == WindowMessages.Xbuttondown)
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                if (exactButton == MouseModifiers.XButton1)
                {
                    return RawMouseEventType.X1ButtonDown;
                }
                else
                {
                    return RawMouseEventType.X2ButtonDown;
                }
            }

            else if (msg == WindowMessages.Xbuttonup)
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                if (exactButton == MouseModifiers.XButton1)
                {
                    return RawMouseEventType.X1ButtonUp;
                }
                else
                {
                    return RawMouseEventType.X2ButtonUp;
                }
            }
            else
            {
                return (RawMouseEventType)msg;
            }
        }

        
    }
}
