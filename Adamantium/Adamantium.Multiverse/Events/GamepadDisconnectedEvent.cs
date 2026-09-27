using Adamantium.Core.Events;
using Adamantium.Multiverse.Payloads;

namespace Adamantium.Multiverse.Events;

/// <summary>A gamepad left its slot; the buttons it held were released that frame. Published on the universe's bus, on
/// its loop.</summary>
public class GamepadDisconnectedEvent : BasicAggregatorEvent<GamepadPayload>
{
}
