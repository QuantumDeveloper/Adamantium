using System;

namespace Adamantium.Win32
{
    [Flags]
    public enum SPIF
    {
        None = 0,
        UpdateIniFile = 1,
        SendChange = 2,
        SendWinIniChange = 2
    }
}