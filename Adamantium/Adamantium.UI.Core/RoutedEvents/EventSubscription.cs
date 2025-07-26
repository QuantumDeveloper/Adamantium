namespace Adamantium.UI.Core.RoutedEvents;

public class EventSubscription
{
   public Delegate Handler { get; set; }
   public bool HandledEventsToo { get; set; }
}