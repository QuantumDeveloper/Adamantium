using Adamantium.Multiverse.Input;

namespace Adamantium.Multiverse.Payloads;

public class GamepadPayload
{
    public GamepadPayload(int slot, Gamepad gamepad)
    {
        Slot = slot;
        Gamepad = gamepad;
    }

    /// <summary>The index the gamepad is read by; it keeps it until it leaves.</summary>
    public int Slot { get; }

    public Gamepad Gamepad { get; }
}
