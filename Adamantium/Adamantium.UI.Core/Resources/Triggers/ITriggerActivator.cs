using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITriggerActivator
{
    void Activate();
    void Deactivate();
}

internal class PropertyTriggerActivator : ITriggerActivator
{
    private readonly ITriggerExecutionContext _context;
    private readonly PropertyTrigger _blueprint;
    private readonly AdamantiumProperty _targetProperty;
    private readonly object _typedValue;

    public PropertyTriggerActivator(ITriggerExecutionContext context, PropertyTrigger blueprint)
    {
        _context = context;
        _blueprint = blueprint;

        _targetProperty = _context.HostComponent.GetProperty(_blueprint.Property);
        if (_targetProperty != null)
        {
            _typedValue = TypeCastFactory.CastFromString(_blueprint.Value, _targetProperty.PropertyType);
        }
    }
    
    public void Activate()
    {
        if (_targetProperty == null) return;

        _context.HostComponent.PropertyChanged += OnPropertyChanged;
        EvaluateCondition();
    }

    public void Deactivate()
    {
        if (_targetProperty == null) return;
                
        _context.HostComponent.PropertyChanged -= OnPropertyChanged;
                
        foreach (var setter in _blueprint.Setters)
        {
            RemoveSetter(setter, _context.HostComponent);
        }
    }
    
    private void OnPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == _targetProperty)
        {
            EvaluateCondition();
        }
    }

    private void EvaluateCondition()
    {
        var currentValue = _context.HostComponent.GetValue(_targetProperty);
        bool conditionMet = Equals(currentValue, _typedValue);
        
        if (conditionMet)
        {
            foreach (var setter in _blueprint.Setters)
            {
                var component = _context.FindTarget(setter.TargetName);
                ApplySetter(setter, (IFundamentalUIComponent)component, _context.Theme);
            }
        }
        else
        {
            foreach (var setter in _blueprint.Setters)
            {
                var component = _context.FindTarget(setter.TargetName);
                RemoveSetter(setter, (IFundamentalUIComponent)component);
            }
        }
    }

    private static void ApplySetter(ISetter setter, IFundamentalUIComponent component, ITheme theme) 
    {
        var prop = component.GetProperty(setter.Property);
        if (prop == null) return;

        var value = TypeCastFactory.CastFromString(setter.Value, prop.PropertyType);
        component.SetTriggerValue(prop, value);
    }

    private static void RemoveSetter(ISetter setter, IFundamentalUIComponent component) 
    {
        var prop = component.GetProperty(setter.Property);
        if (prop == null) return;

        component.ClearValue(prop, ValuePriority.Trigger);
    }

}