using Adamantium.Core.Events;
using Adamantium.Multiverse.Payloads;

namespace Adamantium.Multiverse.Events;

/// <summary>A gamepad took a slot. Published on the universe's bus, on its loop.</summary>
public class GamepadConnectedEvent : BasicAggregatorEvent<GamepadPayload>
{
}
