namespace Adamantium.Multiverse.Input;

/// <summary>
/// Buttons of a gamepad, as flags, named by their place on an Xbox gamepad: <see cref="A"/> is the bottom face button
/// on any gamepad, a DualSense cross included. <see cref="Gamepad.Face"/> says what is printed on them.
/// </summary>
[Flags]
public enum GamepadButton : uint
{
    None = 0,
    A = 1u << 0,
    B = 1u << 1,
    X = 1u << 2,
    Y = 1u << 3,
    LeftShoulder = 1u << 4,
    RightShoulder = 1u << 5,
    Back = 1u << 6,
    Start = 1u << 7,
    LeftThumb = 1u << 8,
    RightThumb = 1u << 9,
    DpadUp = 1u << 10,
    DpadDown = 1u << 11,
    DpadLeft = 1u << 12,
    DpadRight = 1u << 13,

    /// <summary>Upper paddle under the right hand (Xbox Elite P1).</summary>
    Paddle1 = 1u << 14,

    /// <summary>Lower paddle under the right hand (Xbox Elite P2).</summary>
    Paddle2 = 1u << 15,

    /// <summary>Upper paddle under the left hand (Xbox Elite P3).</summary>
    Paddle3 = 1u << 16,

    /// <summary>Lower paddle under the left hand (Xbox Elite P4).</summary>
    Paddle4 = 1u << 17,

    Share = 1u << 18,
    Guide = 1u << 19
}
