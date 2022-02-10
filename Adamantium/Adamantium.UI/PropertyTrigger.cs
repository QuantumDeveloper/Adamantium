using System;
using System.Collections.Generic;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public class PropertyTrigger : ITrigger
{
    private IUIComponent component;
    private Dictionary<AdamantiumProperty, object> values;
    private bool applied;

    public SetterCollection Setters { get; set; }
    
    public AdamantiumProperty Property { get; set; }
    
    public Object Value { get; set; }
    
    public void Apply(IUIComponent control)
    {
        component = control;
        values = new Dictionary<AdamantiumProperty, object>();
        applied = false;
        Property.NotifyChanged += PropertyChanged;
    }

    private void PropertyChanged(object? sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender != component) return;

        if (e.NewValue == Value)
        {
            foreach (var setter in Setters)
            {
                var value = component.GetValue(setter.Property);
                values[setter.Property] = value;
                setter.Apply(component);
            }

            applied = true;
        }
        else if (applied)
        {
            foreach (var (key, value) in values)
            {
                component.SetValue(key, value);
            }
        }
    }
}

public class EventTrigger : ITrigger
{
    private IUIComponent component;
    private Dictionary<AdamantiumProperty, object> values;
    private bool applied;
   
    public RoutedEvent Event { get; set; }
    
    public SetterCollection Setters { get; set; }
    public void Apply(IUIComponent control)
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