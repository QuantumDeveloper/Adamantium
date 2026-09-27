using System;

namespace Adamantium.XInput
{
    [Flags]
    public enum KeystrokeFlags
    {
        None = 0,
        KeyDown = 1,
        KeyUp = 2,
        Repeat = 4
    }
}