using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

public class EventTrigger : TriggerBase
{
    private IObservableComponent component;
   
    public RoutedEvent Event { get; set; }
    
    public bool HandledEventsToo { get; set; }

    public override ITriggerActivator Apply(ITriggerExecutionContext context)
    {
        // if (uiComponent is IObservableComponent observableComponent)
        // {
        //     component = observableComponent;
        //     //observableComponent.AddHandler(Event, EventHandler, HandledEventsToo);
        // }

        return null;
    }

}