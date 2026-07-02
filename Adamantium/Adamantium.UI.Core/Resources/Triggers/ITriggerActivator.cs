using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITriggerActivator
{
    void Activate();
    void Deactivate();

    /// <summary>
    /// Whether this trigger reaches into the templated control's named parts (any setter has a TargetName).
    /// Only such activators need re-pointing when the template is swapped; one that only touches the host's OWN
    /// properties is template-independent - re-pointing it is needless and, for a setter on <c>Template</c> itself,
    /// re-entrant (it would re-swap the template and recurse). See the template-change reevaluation.
    /// </summary>
    bool TargetsTemplateParts { get; }
}

/// <summary>Active while ONE host property equals a value (the WPF Trigger / PropertyTrigger).</summary>
internal sealed class PropertyTriggerActivator : TriggerActivatorBase
{
    private readonly AdamantiumProperty _targetProperty;
    private readonly object _typedValue;

    public PropertyTriggerActivator(ITriggerExecutionContext context, PropertyTrigger blueprint) : base(context, blueprint)
    {
        _targetProperty = context.HostComponent.GetProperty(blueprint.Property);
        if (_targetProperty != null)
            _typedValue = TypeCastFactory.CastFromString(blueprint.Value, _targetProperty.PropertyType);
    }

    public override void Activate()
    {
        if (_targetProperty == null) return;
        Context.HostComponent.PropertyChanged += OnPropertyChanged;
        Evaluate();
    }

    public override void Deactivate()
    {
        if (_targetProperty == null) return;
        Context.HostComponent.PropertyChanged -= OnPropertyChanged;
        TearDown();
    }

    private void OnPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == _targetProperty) Evaluate();
    }

    private void Evaluate() => ApplyState(Equals(Context.HostComponent.GetValue(_targetProperty), _typedValue));
}
