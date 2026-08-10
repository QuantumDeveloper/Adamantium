using System.ComponentModel;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Live connection for <c>{Self ...}</c>: binds a target property to another property (or dotted path) on the SAME
/// element. No tree walk - the source is always the target itself. Shares the dotted-path / converter / fallback
/// pipeline with <see cref="AncestorBindingExpression"/>.
/// </summary>
public class SelfBindingExpression : BindingExpressionBase
{
    private readonly Self _def;
    internal ValuePriority Priority { get; set; } = ValuePriority.Binding;
    private AdamantiumProperty _sourceProperty;
    private string[] _segments = [];
    private INotifyPropertyChanged _leafOwner;

    public SelfBindingExpression(IAdamantiumComponent target, AdamantiumProperty targetProperty, Self def)
    {
        Target = target;
        TargetProperty = targetProperty;
        _def = def;
    }

    public override void EstablishConnection()
    {
        CloseConnection();
        if (Target == null) return;

        _segments = _def.Path?.Split('.') ?? [];
        _sourceProperty = _segments.Length > 0 ? Target.GetProperty(_segments[0]) : null;

        var firstHopExists = _sourceProperty != null
            || (_segments.Length > 0 && Target.GetType().GetProperty(_segments[0]) != null);
        if (!firstHopExists)
        {
            Status = BindingStatus.PathError;
            BindingTrace.Log($"{{Self}} on {Target.GetType().Name}.{TargetProperty?.Name}: no '{_def.Path}' property on the element.");
            return;
        }

        Target.PropertyChanged += OnPropertyChanged;   // covers the source hop AND (TwoWay) the target property
        HookLeafOwner();
        UpdateTarget();
        Status = BindingStatus.Active;
    }

    public override void CloseConnection()
    {
        BindingUpdateQueue.Remove(this);
        if (Target != null) Target.PropertyChanged -= OnPropertyChanged;
        UnhookLeafOwner();
        _sourceProperty = null;
        _segments = [];
    }

    private void HookLeafOwner()
    {
        if (_segments.Length <= 1) return;
        _leafOwner = RelativeBindingPipeline.LeafOwner(Target, _sourceProperty, _segments) as INotifyPropertyChanged;
        if (_leafOwner != null) _leafOwner.PropertyChanged += OnLeafOwnerChanged;
    }

    private void UnhookLeafOwner()
    {
        if (_leafOwner != null) _leafOwner.PropertyChanged -= OnLeafOwnerChanged;
        _leafOwner = null;
    }

    private void OnLeafOwnerChanged(object sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _segments[^1]) ScheduleUpdate();
    }

    private void OnPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == _sourceProperty)
        {
            if (_segments.Length > 1) { UnhookLeafOwner(); HookLeafOwner(); }
            ScheduleUpdate();
        }
        else if (_def.Mode == BindingMode.TwoWay && _segments.Length == 1 && e.Property == TargetProperty)
        {
            UpdateSource();
        }
    }

    public override void UpdateTarget()
    {
        if (Target == null || TargetProperty == null) return;
        var raw = RelativeBindingPipeline.Walk(Target, _sourceProperty, _segments);
        var value = RelativeBindingPipeline.Produce(raw, _def.Converter, _def.ConverterParameter,
            TargetProperty.PropertyType, _def.FallbackValue, _def.TargetNullValue);
        if (ReferenceEquals(value, RelativeBindingPipeline.Unset)) return;
        Target.SetValue(TargetProperty, value, Priority);
    }

    public override void UpdateSource()
    {
        if (_sourceProperty == null || _def.Mode != BindingMode.TwoWay || _segments.Length != 1) return;
        // Write-back is authoritative - push at Local so an existing Local/Style value on the source property doesn't mask it.
        var value = RelativeBindingPipeline.ConvertBack(Target.GetValue(TargetProperty), _def.Converter, _def.ConverterParameter, _sourceProperty.PropertyType);
        if (TryCoerce(value, _sourceProperty.PropertyType, out var coerced))
            Target.SetValue(_sourceProperty, coerced, ValuePriority.Local);
    }
}
