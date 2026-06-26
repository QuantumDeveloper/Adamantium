using System.Collections.Generic;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>Active only while ALL of a <see cref="MultiTrigger"/>'s conditions hold at once. Watches every condition
/// property on the host and re-evaluates the AND of them on any change.</summary>
internal sealed class MultiTriggerActivator : TriggerActivatorBase
{
    private readonly (AdamantiumProperty Property, object TypedValue)[] _conditions;

    public MultiTriggerActivator(ITriggerExecutionContext context, MultiTrigger blueprint) : base(context, blueprint)
    {
        var host = context.HostComponent;
        List<(AdamantiumProperty, object)> resolved = [];
        foreach (var condition in blueprint.Conditions)
        {
            var property = host.GetProperty(condition.Property);
            if (property == null) return;   // an unresolvable condition leaves _conditions null -> the trigger is inert
            resolved.Add((property, TypeCastFactory.CastFromString(condition.Value, property.PropertyType)));
        }
        _conditions = resolved.Count > 0 ? resolved.ToArray() : null;
    }

    public override void Activate()
    {
        if (_conditions == null) return;
        Context.HostComponent.PropertyChanged += OnPropertyChanged;
        Evaluate();
    }

    public override void Deactivate()
    {
        if (_conditions == null) return;
        Context.HostComponent.PropertyChanged -= OnPropertyChanged;
        TearDown();
    }

    private void OnPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        foreach (var condition in _conditions)
        {
            if (e.Property == condition.Property)
            {
                Evaluate();
                return;
            }
        }
    }

    private void Evaluate()
    {
        var host = Context.HostComponent;
        foreach (var condition in _conditions)
        {
            if (!Equals(host.GetValue(condition.Property), condition.TypedValue))
            {
                ApplyState(false);
                return;
            }
        }
        ApplyState(true);
    }
}
