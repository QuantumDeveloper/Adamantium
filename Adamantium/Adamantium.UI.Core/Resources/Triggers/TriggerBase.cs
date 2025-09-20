using Adamantium.UI.Core.Data;

namespace Adamantium.UI.Core.Resources.Triggers;

public abstract class TriggerBase : ITrigger
{
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
                if (!theme.TryGetResource(resourceReference.Name, out var resource))
                    throw new ResourceNotFoundException(
                        $"Resource {resourceReference.Name} is not found for theme: {theme.Name} and control: {component.GetType().Name}");
                
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

    public void Add(ISetter setter)
    {
        Setters ??= [];
        Setters.Add(setter);
    }

    public abstract ITriggerActivator Apply(ITriggerExecutionContext context);
}