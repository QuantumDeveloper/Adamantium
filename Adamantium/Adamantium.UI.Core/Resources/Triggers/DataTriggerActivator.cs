using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>Active while a <c>{Binding}</c> on the host resolves to a given value (the WPF DataTrigger). The binding runs
/// in PRODUCER mode - it exposes its converted value via <see cref="BindingExpressionBase.ProducedValue"/> rather than
/// writing a target property - and the trigger re-evaluates whenever that value, or the host's DataContext, changes.</summary>
internal sealed class DataTriggerActivator : TriggerActivatorBase
{
    private readonly DataTrigger _blueprint;
    private readonly IFundamentalUIComponent _host;
    private readonly BindingExpressionBase _expression;

    public DataTriggerActivator(ITriggerExecutionContext context, DataTrigger blueprint) : base(context, blueprint)
    {
        _blueprint = blueprint;
        _host = context.HostComponent;
        if (_host != null && blueprint.Binding != null)
            _expression = new BindingExpression(_host, (AdamantiumProperty)null, blueprint.Binding);
    }

    public override void Activate()
    {
        if (_expression == null) return;
        _expression.ValueChanged += OnProducedValueChanged;
        _host.DataContextChanged += OnDataContextChanged;
        _expression.EstablishConnection();   // initial produce -> OnProducedValueChanged -> Evaluate
    }

    public override void Deactivate()
    {
        if (_expression == null) return;
        _expression.ValueChanged -= OnProducedValueChanged;
        _host.DataContextChanged -= OnDataContextChanged;
        _expression.CloseConnection();
        TearDown();
    }

    // The tree is usually built before its DataContext is assigned (and it can change later), so re-resolve the binding
    // against the new context - EstablishConnection is idempotent and re-produces the value.
    private void OnDataContextChanged(object sender, AdamantiumPropertyChangedEventArgs e) => _expression.EstablishConnection();

    private void OnProducedValueChanged(BindingExpressionBase _) => Evaluate();

    private void Evaluate() => ApplyState(Matches(_expression.ProducedValue));

    // Compare the bound value to the trigger's declared Value. The bound value's type is only known at runtime, so a
    // string Value from markup (e.g. Value="True") is converted to that type before comparing; an already-typed Value
    // (set in code) is compared directly.
    private bool Matches(object produced)
    {
        var expected = _blueprint.Value;
        if (produced == null) return expected == null;
        if (expected == null) return false;
        if (produced.GetType().IsInstanceOfType(expected)) return Equals(produced, expected);
        return Equals(produced, TypeCastFactory.CastFromString(expected, produced.GetType()));
    }
}
