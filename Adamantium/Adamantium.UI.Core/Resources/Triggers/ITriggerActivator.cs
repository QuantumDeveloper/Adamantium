using Adamantium.UI.Core.Data;
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

    // What each setter currently has applied: the EXACT part it was pushed onto plus how to undo it (clear the trigger
    // value, drop any {ThemeResource}/{TemplateBinding} subscription). Tracking the real target - rather than re-asking
    // FindTarget at teardown - is what keeps cleanup correct across a runtime template swap, where FindTarget would by
    // then return the NEW part and leave the old one dirty and still subscribed (a leak). Re-evaluating after a swap
    // (Deactivate+Activate) therefore undoes the old parts precisely and re-applies onto the new ones.
    private readonly System.Collections.Generic.Dictionary<ISetter, (IFundamentalUIComponent Target, System.Action Teardown)> _applied = new();

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
        TearDownAll();
        _conditionMet = false;
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
                if (conditionMet)
                    ApplySetter(setter);
                else
                    RemoveSetter(setter);
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

    // A trigger setter's Value is the same family of markers a style setter resolves (see Setter.Apply): a
    // {ResourceReference} (one-shot palette lookup), a {ThemeResource} (live link to the active theme's accent/focus),
    // or a {TemplateBinding} (live link to a property of the templated control - this is what lets ONE template serve
    // both the base and Accent button, the variants differing only in the control's state brushes). A plain string is
    // parsed. Without this a marker reached CastFromString and crashed (a ResourceReference stringified into the brush
    // parser) the moment a hover/press/focus trigger fired.
    private void ApplySetter(ISetter setter)
    {
        var component = _context.FindTarget(setter.TargetName) as IFundamentalUIComponent;

        // Already applied? Same part -> nothing to do (live markers keep themselves current; static ones don't change).
        // Different part (the template was swapped under us) -> undo the old one before re-targeting the new part.
        if (_applied.TryGetValue(setter, out var existing))
        {
            if (ReferenceEquals(existing.Target, component)) return;
            existing.Teardown();
            _applied.Remove(setter);
        }

        if (component == null) return;
        var prop = component.GetProperty(setter.Property);
        if (prop == null) return;

        switch (setter.Value)
        {
            case ResourceReference resourceReference:
                if (_context.Theme != null && _context.Theme.TryGetResource(resourceReference.Name, out var resource))
                    component.SetTriggerValue(prop, resource);
                _applied[setter] = (component, () => component.ClearValue(prop, ValuePriority.Trigger));
                break;

            case ThemeResource themeResource:
                themeResource.Apply(component, setter.Property, ValuePriority.Trigger);
                _applied[setter] = (component, () => ThemeResource.Remove(component, setter.Property, ValuePriority.Trigger));
                break;

            case TemplateBinding templateBinding:
                ApplyTemplateBinding(setter, component, prop, templateBinding);
                break;

            default:
                component.SetTriggerValue(prop, TypeCastFactory.CastFromString(setter.Value, prop.PropertyType));
                _applied[setter] = (component, () => component.ClearValue(prop, ValuePriority.Trigger));
                break;
        }
    }

    // {TemplateBinding Path} inside a trigger reads Path from the templated control (the host) and pushes it onto the
    // part at Trigger priority, then tracks the host so a later change (e.g. a runtime accent swap) flows through live.
    private void ApplyTemplateBinding(ISetter setter, IFundamentalUIComponent component, AdamantiumProperty targetProperty, TemplateBinding templateBinding)
    {
        var host = _context.HostComponent;
        var sourceProperty = host.GetProperty(templateBinding.Path);
        if (sourceProperty == null) return;

        component.SetTriggerValue(targetProperty, host.GetValue(sourceProperty));

        void OnHostPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
        {
            if (e.Property == sourceProperty) component.SetTriggerValue(targetProperty, e.NewValue);
        }

        host.PropertyChanged += OnHostPropertyChanged;
        _applied[setter] = (component, () =>
        {
            host.PropertyChanged -= OnHostPropertyChanged;
            component.ClearValue(targetProperty, ValuePriority.Trigger);
        });
    }

    private void RemoveSetter(ISetter setter)
    {
        if (_applied.Remove(setter, out var applied))
            applied.Teardown();
    }

    private void TearDownAll()
    {
        foreach (var applied in _applied.Values)
            applied.Teardown();
        _applied.Clear();
    }
}
