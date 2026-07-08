using System.Collections.Generic;
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
                // Tree-scoped resolve from the triggered element. If it misses (e.g. the trigger is evaluated before the
                // element is in a rooted tree, so a Local resource on an ancestor isn't reachable yet) skip rather than
                // throw - a trigger re-evaluates when its condition next changes, by which point the tree is rooted. This
                // mirrors the trigger activator, which also skips on miss.
                if (theme.TryGetResource(component, resourceReference.Name, out var resource))
                    component.SetValue(setterProperty, resource, ValuePriority.Trigger);
                break;
            case ThemeResource themeResource:
                themeResource.Apply(component, setterProperty, ValuePriority.Trigger);
                break;
            case Ancestor ancestor:
                ancestor.Apply(component, setterProperty, ValuePriority.Trigger);
                break;
            case Self self:
                self.Apply(component, setterProperty, ValuePriority.Trigger);
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
            case ThemeResource:
                ThemeResource.Remove(component, setterProperty, ValuePriority.Trigger);
                break;
            case Ancestor:
            case Self:
                component.RemoveBinding(setterProperty);
                break;
            default:
                component.ClearValue(setterProperty, ValuePriority.Trigger);
                break;
        }
    }

    public SetterCollection Setters { get; set; }

    /// <summary>Actions run when the trigger's condition becomes true (e.g. start an animation). WPF EnterActions analog.</summary>
    public List<ITriggerAction> EnterActions { get; } = new();

    /// <summary>Actions run when the trigger's condition becomes false again. WPF ExitActions analog.</summary>
    public List<ITriggerAction> ExitActions { get; } = new();

    public void Add(ISetter setter)
    {
        Setters ??= [];
        Setters.Add(setter);
    }

    public abstract ITriggerActivator Apply(ITriggerExecutionContext context);
}