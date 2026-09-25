using Adamantium.Core.Events;

namespace Adamantium.Game;

/// <summary>
/// A universe's own event bus: its outputs are made, removed and asked to change through it. Told apart from the
/// application's bus by its type.
/// </summary>
public interface IUniverseEventAggregator : IEventAggregator
{
}
