using System;

namespace Adamantium.Win32
{
    [Flags]
    public enum SPI
    {
        GetMouse = 0x0003,
        GetMouseSpeed = 0x0070
    }
}