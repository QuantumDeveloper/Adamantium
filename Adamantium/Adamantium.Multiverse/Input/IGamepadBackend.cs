namespace Adamantium.Multiverse.Input;

/// <summary>
/// A platform's source of gamepads. The host registers the one its platform has; with none registered there are no
/// gamepads.
/// </summary>
public interface IGamepadBackend
{
    /// <summary>The gamepads connected as of the last <see cref="Update"/>.</summary>
    IReadOnlyList<Gamepad> Gamepads { get; }

    /// <summary>Takes in the gamepads that arrived and drops the ones that left. Called once per frame, before any
    /// gamepad is read.</summary>
    void Update();
}
