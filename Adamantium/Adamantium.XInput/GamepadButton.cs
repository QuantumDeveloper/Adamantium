using System;

namespace Adamantium.XInput
{
    [Flags]
    public enum GamepadButton : short
    {
        None = 0,
        DpadUp = 1,
        DpadDown = 2,
        DpadLeft = 4,
        DpadRight = 8,
        Start = 16,
        Back = 32,
        LeftThumb = 64,
        RightThumb = 128,
        LeftShoulder = 256,
        RightShoulder = 512,
        A = 4096,
        B = 8192,
        X = 16384,
        Y = short.MinValue
    }
}