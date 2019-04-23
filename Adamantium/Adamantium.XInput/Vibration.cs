using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vibration
    {
        public ushort LeftMotorSpeed;

        public ushort RightMotorSpeed;
    }
}