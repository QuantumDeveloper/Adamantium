using System.Runtime.InteropServices;

namespace Adamantium.XInput
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BatteryInformation
    {
        public BatteryType BatteryType;

        public BatteryLevel BatteryLevel;
    }
}