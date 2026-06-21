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

        if (_blueprint.Setters == null) return;
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

    private bool _conditionMet;

    private void EvaluateCondition()
    {
        var currentValue = _context.HostComponent.GetValue(_targetProperty);
        bool conditionMet = Equals(currentValue, _typedValue);

        // Setters apply/remove idempotently for as long as the condition holds.
        if (_blueprint.Setters != null)
        {
            foreach (var setter in _blueprint.Setters)
            {
                var component = _context.FindTarget(setter.TargetName);
                if (conditionMet)
                    ApplySetter(setter, (IFundamentalUIComponent)component, _context.Theme);
                else
                    RemoveSetter(setter, (IFundamentalUIComponent)component);
            }
        }

        // Enter/Exit actions (e.g. animations) fire only on the EDGE - when the condition crosses, not on every change.
        if (conditionMet && !_conditionMet)
            InvokeActions(_blueprint.EnterActions);
        else if (!conditionMet && _conditionMet)
            InvokeActions(_blueprint.ExitActions);
        _conditionMet = conditionMet;
    }

    private void InvokeActions(System.Collections.Generic.IEnumerable<ITriggerAction> actions)
    {
        foreach (var action in actions)
            action.Invoke(_context);
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