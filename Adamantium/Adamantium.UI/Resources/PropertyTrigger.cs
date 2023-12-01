using System;
using Adamantium.UI.Controls;
using Adamantium.UI.Data;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Resources;

public abstract class TriggerBase : ITrigger
{
    protected ITheme Theme;
    protected TriggerBase()
    {
        
    }
    protected void ApplySetter(ISetter setter, IFundamentalUIComponent component, ITheme theme)
    {
        var setterProperty = setter.Property;
        var setterValue = setter.Value; 
        switch (setterValue)
        {
            case BindingBase binding:
                component.SetBinding(setterProperty, binding);
                break;
            case ResourceReference resourceReference:
                var resource = theme.Resources[resourceReference.Name];
                component.SetValue(setterProperty, resource, ValuePriority.Trigger);
                break;
            default:
                var prop = AdamantiumPropertyMap.FindRegistered(component.GetType(), setterProperty);
                var value = TypeCastFactory.CastFromString(setterValue, prop.PropertyType);
                component.SetValue(prop, value, ValuePriority.Trigger);
                break;
        }
    }
    
    protected void RemoveSetter(ISetter setter, IFundamentalUIComponent component)
    {
        var setterProperty = setter.Property;
        switch (setter.Value)
        {
            case BindingBase binding:
                component.RemoveBinding(setterProperty);
                break;
            default:
                component.ClearValue(setterProperty, ValuePriority.Trigger);
                break;
        }
    }

    public SetterCollection Setters { get; set; }

    public abstract void Apply(IFundamentalUIComponent uiComponent, ITheme theme);

    public abstract void Remove(IFundamentalUIComponent uiComponent);
}

public class PropertyTrigger : TriggerBase
{
    private IFundamentalUIComponent component;
    

    public SetterCollection Setters { get; set; }
    
    public AdamantiumProperty Property { get; set; }
    
    public Object Value { get; set; }
    
    public override void Apply(IFundamentalUIComponent uiComponent, ITheme theme)
    {
        component = uiComponent;
        Theme = theme;
        Property.NotifyChanged += PropertyChanged;
    }

    public override void Remove(IFundamentalUIComponent uiComponent)
    {
        Property.NotifyChanged -= PropertyChanged;
        foreach (var setter in Setters)
        {
            RemoveSetter(setter, uiComponent);
        }
    }

    private void PropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender != component) return;

        if (e.NewValue != Value) return;
        
        foreach (var setter in Setters)
        {
            ApplySetter(setter, component, Theme);
        }
    }
}
