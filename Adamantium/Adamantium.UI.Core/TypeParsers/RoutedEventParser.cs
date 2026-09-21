using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.TypeParsers;

/// <summary>Turns an AUML attribute string (e.g. <c>Event="Loaded"</c>) into the registered <see cref="RoutedEvent"/>,
/// so a markup <c>EventTrigger</c> can name the event to fire on. Resolution is by name via <see cref="EventManager"/>.</summary>
public class RoutedEventParser : ITypeParser<RoutedEvent>
{
    public RoutedEvent Parse(string value) => EventManager.FindRoutedEvent(value);
}
