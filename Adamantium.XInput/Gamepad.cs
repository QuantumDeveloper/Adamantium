using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad
    {
        public const short LeftThumbDeadZone = 7849;

        public const short RightThumbDeadZone = 8689;

        public const byte TriggerThreshold = 30;

        public GamepadButton Buttons;

        public byte LeftTrigger;

        public byte RightTrigger;

        public short LeftThumbX;

        public short LeftThumbY;

        public short RightThumbX;

        public short RightThumbY;

        public override string ToString()
        {
            return
                $"LeftTrigger: {LeftTrigger} RightTrigger: {RightTrigger} LeftThumbX {LeftThumbX} LeftThumbY {LeftThumbY} " +
                $"RightThumbX {RightThumbX} RightThumbY {RightThumbY} Buttons: {Buttons}";
        }
    }
}