using Adamantium.Multiverse.Input;

namespace Adamantium.EngineTests;

/// <summary>A gamepad whose reading the test sets.</summary>
public class FakeGamepad : Gamepad
{
    public GamepadState State;

    public override GamepadState GetState()
    {
        return State;
    }
}
