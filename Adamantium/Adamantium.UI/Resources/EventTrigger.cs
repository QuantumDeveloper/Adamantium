using System;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Resources;

public class EventTrigger : TriggerBase
{
    private IObservableComponent component;
   
    public RoutedEvent Event { get; set; }
    
    public bool HandledEventsToo { get; set; }
    
    public SetterCollection Setters { get; set; }

    public override void Apply(IFundamentalUIComponent uiComponent, ITheme theme)
    {
        Theme = theme;
        if (uiComponent is IObservableComponent observableComponent)
        {
            component = observableComponent;
            observableComponent.AddHandler(Event, EventHandler, HandledEventsToo);
        }
    }

    public override void Remove(IFundamentalUIComponent uiComponent)
    {
        if (uiComponent is IObservableComponent observableComponent)
        {
            observableComponent.RemoveHandler(Event, EventHandler);
            foreach (var setter in Setters)
            {
                RemoveSetter(setter, component);
            }
        }
    }

    private void EventHandler(object sender, RoutedEventArgs e)
    {
        foreach (var setter in Setters)
        {
            ApplySetter(setter, component, Theme);
        }
    }

}