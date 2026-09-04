using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITriggerActivator
{
    void Activate();
    void Deactivate();

    /// <summary>The host has left the visual tree: stop anything the trigger left RUNNING, but keep the condition and
    /// the applied setters, because the host is expected back. Deactivate is the wrong tool here - it forgets the state
    /// - and doing nothing is what leaked: a looping loading pulse whose page was navigated away kept ticking for the
    /// rest of the session, one orphan per indicator, and the frame paid for all of them.</summary>
    void SuspendActions();

    /// <summary>...and the host is back: re-run what suspending stopped, if the condition still holds. A looping
    /// animation resumes at the phase it was suspended on (RunAnimationAction keeps it), so a returning page picks its
    /// pulse up mid-stride instead of snapping back to the start.</summary>
    void ResumeActions();

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
    private readonly PropertyTrigger _blueprint;
    private AdamantiumProperty _targetProperty;
    private object _typedValue;
    private IAdamantiumComponent _source;

    public PropertyTriggerActivator(ITriggerExecutionContext context, PropertyTrigger blueprint) : base(context, blueprint)
    {
        _blueprint = blueprint;
    }

    // Resolved HERE and not in the constructor: with a SourceName the part may not exist yet (the template is applied
    // after the style attaches), and the template's own first build re-points every activator that reaches the parts.
    public override void Activate()
    {
        _source = string.IsNullOrEmpty(_blueprint.SourceName)
            ? Context.HostComponent
            : Context.FindTarget(_blueprint.SourceName);
        if (_source == null) return;

        _targetProperty = _source.GetProperty(_blueprint.Property);
        if (_targetProperty == null) { _source = null; return; }

        _typedValue = TypeCastFactory.CastFromString(_blueprint.Value, _targetProperty.PropertyType);
        _source.PropertyChanged += OnPropertyChanged;
        Evaluate();
    }

    // Unsubscribes from the instance it SUBSCRIBED to, not from whatever the name resolves to now: after a template
    // swap that is a different part, and the discarded one would keep the handler forever.
    public override void Deactivate()
    {
        if (_source == null) return;
        _source.PropertyChanged -= OnPropertyChanged;
        _source = null;
        TearDown();
    }

    private void OnPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == _targetProperty) Evaluate();
    }

    private void Evaluate() => ApplyState(Equals(_source.GetValue(_targetProperty), _typedValue));
}
