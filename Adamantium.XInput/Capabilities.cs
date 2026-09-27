using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Capabilities
    {
        public DeviceType Type;
        public DeviceSubType SubType;
        public DeviceCapabilities Flags;
        public Gamepad Gamepad;
        public Vibration Vibration;
    }
}