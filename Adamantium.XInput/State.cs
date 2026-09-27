using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct State
    {
        public int PacketNumber;

        public Gamepad Gamepad;

        public override string ToString()
        {
            return Gamepad.ToString();
        }
    }
}