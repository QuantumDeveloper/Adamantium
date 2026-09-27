using Adamantium.Multiverse.Input;
using EngineGamepad = Adamantium.Multiverse.Input.Gamepad;

namespace Adamantium.XInput;

/// <summary>
/// Gamepads through XInput 1.4: four Xbox-compatible slots, without the Elite paddles. An empty slot is asked about once a
/// second, because asking an empty one is slow; a taken one every frame.
/// </summary>
public sealed class XInputGamepadBackend : IGamepadBackend
{
    private const long EmptySlotPollMilliseconds = 1000;

    private readonly object sync = new();
    private readonly XInputGamepad[] slots;
    private readonly long[] nextPoll;
    private EngineGamepad[] connected = [];

    public XInputGamepadBackend()
    {
        if (!XBoxController.IsSupported)
        {
            slots = [];
            nextPoll = [];
            return;
        }

        slots = new XInputGamepad[XBoxController.MaxControllers];
        nextPoll = new long[XBoxController.MaxControllers];
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = new XInputGamepad(new XBoxController((UserIndex)i));
        }
    }

    public IReadOnlyList<EngineGamepad> Gamepads => Volatile.Read(ref connected);

    public void Update()
    {
        lock (sync)
        {
            var now = Environment.TickCount64;
            var changed = false;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!slot.IsConnected && now < nextPoll[i])
                {
                    continue;
                }

                var isConnected = slot.Controller.IsConnected;
                if (!isConnected)
                {
                    nextPoll[i] = now + EmptySlotPollMilliseconds;
                }

                if (isConnected != slot.IsConnected)
                {
                    slot.IsConnected = isConnected;
                    changed = true;
                }
            }

            if (changed)
            {
                Volatile.Write(ref connected, slots.Where(slot => slot.IsConnected).ToArray<EngineGamepad>());
            }
        }
    }
}
