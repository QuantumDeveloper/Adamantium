using Adamantium.Mathematics;

namespace Adamantium.Multiverse.Input;

/// <summary>
/// One reading of a gamepad: sticks are -1..1 with up and right positive, triggers 0..1.
/// </summary>
public struct GamepadState
{
    public bool IsConnected;

    public GamepadButton Buttons;

    public Vector2F LeftThumb;

    public Vector2F RightThumb;

    public float LeftTrigger;

    public float RightTrigger;
}
