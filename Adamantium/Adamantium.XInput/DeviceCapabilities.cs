using System;

namespace Adamantium.XInput
{
    [Flags]
    public enum DeviceCapabilities : short
    {
        None = 0,
        FfbSupported = 1,
        Wireless = 2,
        VoiceSupported = 4,
        PmdSupported = 8,
        NoNavigation = 16
    }
}