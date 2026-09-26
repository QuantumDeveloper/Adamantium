using System.Collections.Generic;
using Adamantium.Multiverse.Input;

namespace Adamantium.EngineTests;

/// <summary>A backend whose connected gamepads the test puts in and takes out.</summary>
public class FakeGamepadBackend : IGamepadBackend
{
    public List<Gamepad> Connected { get; } = [];

    public int Updates { get; private set; }

    public IReadOnlyList<Gamepad> Gamepads => Connected;

    public void Update()
    {
        Updates++;
    }
}
