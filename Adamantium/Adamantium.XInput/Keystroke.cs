using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Keystroke
    {
        public GamepadKeyCode VirtualKey;

        public char Unicode;

        public KeystrokeFlags Flags;

        public UserIndex UserIndex;

        public byte HidCode;

        public override string ToString()
        {
            return $"User Index: {UserIndex} Gamepad key code: {VirtualKey} Unicode: {Unicode} Keystroke flags: {Flags} HidCode: {HidCode}";
        }
    }
}