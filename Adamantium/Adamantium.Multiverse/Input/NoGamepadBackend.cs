namespace Adamantium.Multiverse.Input;

/// <summary>
/// The backend of a platform that has no gamepad support: nothing is ever connected.
/// </summary>
public sealed class NoGamepadBackend : IGamepadBackend
{
    public IReadOnlyList<Gamepad> Gamepads => [];

    public void Update()
    {
    }
}
