using System.Collections.Generic;
using Adamantium.UI.Input;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public class EventTrigger : ITrigger
{
    private IInputComponent component;
    private Dictionary<AdamantiumProperty, object> values;
    private bool applied;
   
    public RoutedEvent Event { get; set; }
    
    public SetterCollection Setters { get; set; }
    public void Apply(IInputComponent control)
    {
        component = control;
        values = new Dictionary<AdamantiumProperty, object>();
        applied = false;
        component.AddHandler(Event, new RoutedEventHandler(EventHandler));
    }

    private void EventHandler(object sender, RoutedEventArgs e)
    {
        
    }
}