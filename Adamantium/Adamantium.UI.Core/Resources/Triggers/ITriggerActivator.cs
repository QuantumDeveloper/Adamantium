using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITriggerActivator
{
    void Activate();
    void Deactivate();
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
