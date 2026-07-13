using System;
using System.Collections.Generic;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// Shared machinery for a live trigger: applies/removes the trigger's setters and fires its Enter/Exit actions as the
/// condition crosses, with correct teardown across a runtime template swap. Subclasses decide only WHAT the condition is
/// (one property, or several) and which host properties to watch, then call <see cref="ApplyState"/>.
/// </summary>
internal abstract class TriggerActivatorBase : ITriggerActivator
{
    protected readonly ITriggerExecutionContext Context;
    private readonly TriggerBase _trigger;

    // What each setter currently has applied: the EXACT part it was pushed onto plus how to undo it (clear the trigger
    // value, drop any {ThemeResource}/{TemplateBinding} subscription). Tracking the real target - rather than re-asking
    // FindTarget at teardown - keeps cleanup correct across a runtime template swap, where FindTarget would by then
    // return the NEW part and leave the old one dirty and still subscribed (a leak).
    private readonly Dictionary<ISetter, (IFundamentalUIComponent Target, Action Teardown)> _applied = new();
    private bool _conditionMet;

    protected TriggerActivatorBase(ITriggerExecutionContext context, TriggerBase trigger)
    {
        Context = context;
        _trigger = trigger;

        if (trigger.Setters != null)
        {
            foreach (var setter in trigger.Setters)
            {
                if (!string.IsNullOrEmpty(setter.TargetName))
                {
                    TargetsTemplateParts = true;
                    break;
                }
            }
        }
    }

    // A setter with a TargetName reaches a template PART; one with none touches the host's own property. Fixed for the
    // trigger's lifetime - the setters don't change - so it's computed once.
    public bool TargetsTemplateParts { get; }

    public abstract void Activate();

    public abstract void Deactivate();

    /// <summary>Subclasses call this on activation and whenever a watched property changes, passing whether the
    /// condition (ALL conditions, for a MultiTrigger) now holds. Setters apply/remove idempotently for as long as it
    /// holds; Enter/Exit actions fire only on the EDGE - when the condition crosses, not on every change.</summary>
    protected void ApplyState(bool conditionMet)
    {
        if (_trigger.Setters != null)
        {
            foreach (var setter in _trigger.Setters)
            {
                if (conditionMet) ApplySetter(setter);
                else RemoveSetter(setter);
            }
        }

        if (conditionMet && !_conditionMet) InvokeActions(_trigger.EnterActions);
        else if (!conditionMet && _conditionMet) InvokeActions(_trigger.ExitActions);
        _conditionMet = conditionMet;
    }

    /// <summary>Undo every applied setter and reset the edge state (called on Deactivate).</summary>
    protected void TearDown()
    {
        // Snapshot and clear state BEFORE running teardowns: a teardown clears a trigger value, whose property-changed
        // callback can re-enter this activator (RemoveSetter/ApplySetter) and mutate _applied - iterating it live throws
        // "Collection was modified". This is what broke a runtime theme swap on the first triggered control it hit.
        (IFundamentalUIComponent Target, Action Teardown)[] teardowns = [.. _applied.Values];
        var wasConditionMet = _conditionMet;
        _applied.Clear();
        _conditionMet = false;
        foreach (var applied in teardowns)
            applied.Teardown();

        // Deactivated while the condition still HELD (a theme/template swap - the state never crossed back), so the exit
        // edge never came and the ExitActions never ran. An enter-action that left something RUNNING must still be undone
        // here, or a looping animation (a loading pulse) keeps ticking against the discarded brush/part for the rest of
        // the session - one orphan per swap. Only the undoable ones: re-running the ExitActions wholesale would instead
        // START their animations (an auto-hide scrollbar fades OUT from its ExitActions) on parts already thrown away.
        if (!wasConditionMet || _trigger.EnterActions == null) return;
        foreach (var action in _trigger.EnterActions)
            (action as IUndoableTriggerAction)?.Undo(Context);
    }

    private void InvokeActions(IEnumerable<ITriggerAction> actions)
    {
        foreach (var action in actions)
            action.Invoke(Context);
    }

    // A trigger setter's Value is the same family of markers a style setter resolves (see Setter.Apply): a
    // {ResourceReference} (one-shot palette lookup), a {ThemeResource} (live link to the active theme's accent/focus),
    // or a {TemplateBinding} (live link to a property of the templated control - this is what lets ONE template serve
    // both the base and Accent button). A plain string is parsed.
    private void ApplySetter(ISetter setter)
    {
        var component = Context.FindTarget(setter.TargetName) as IFundamentalUIComponent;

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

        // The setter is the stack TOKEN: each contribution is tracked independently, so two triggers on the same part
        // property stack (last applied wins) and removing one restores the other instead of clearing the slot.
        switch (setter.Value)
        {
            case ResourceReference resourceReference:
                // Context.Theme is captured when the trigger is wired up - which for an inline <X.Styles> happens during
                // construction, before the element is themed, so it can be null. Fall back to the current theme (the
                // trigger fires later, once live). Resolve tree-scoped from the target so a Local resource is reachable.
                var theme = Context.Theme ?? UIAppContext.Current?.ThemeManager?.CurrentTheme;
                if (theme != null && theme.TryGetResource(component, resourceReference.Name, out var resource))
                    component.SetTriggerValue(prop, resource, setter);
                _applied[setter] = (component, () => component.ClearTriggerValue(prop, setter));
                break;

            case ThemeResource themeResource:
                themeResource.Apply(component, setter.Property, ValuePriority.Trigger, setter);
                _applied[setter] = (component, () => ThemeResource.Remove(component, setter.Property, ValuePriority.Trigger, setter));
                break;

            case ObservableResource observableResource:
                observableResource.Apply(component, setter.Property, ValuePriority.Trigger, setter);
                _applied[setter] = (component, () => ObservableResource.Remove(component, setter.Property, ValuePriority.Trigger, setter));
                break;

            case TemplateBinding templateBinding:
                ApplyTemplateBinding(setter, component, prop, templateBinding);
                break;

            // {Ancestor}/{Self} as a part-targeting trigger value: wire a live binding at Trigger priority, torn down on
            // exit. NOTE: unlike the per-token stack above, two triggers writing the SAME part property via a relative
            // binding don't stack (removal clears the whole binding slot) - fine for the normal single-trigger case.
            case Ancestor ancestor:
                ancestor.Apply(component, setter.Property, ValuePriority.Trigger);
                _applied[setter] = (component, () => component.RemoveBinding(setter.Property));
                break;

            case Self self:
                self.Apply(component, setter.Property, ValuePriority.Trigger);
                _applied[setter] = (component, () => component.RemoveBinding(setter.Property));
                break;

            default:
                component.SetTriggerValue(prop, TypeCastFactory.CastFromString(setter.Value, prop.PropertyType), setter);
                _applied[setter] = (component, () => component.ClearTriggerValue(prop, setter));
                break;
        }
    }

    // {TemplateBinding Path} inside a trigger reads Path from the templated control (the host) and pushes it onto the
    // part at Trigger priority, then tracks the host so a later change (e.g. a runtime accent swap) flows through live.
    private void ApplyTemplateBinding(ISetter setter, IFundamentalUIComponent component, AdamantiumProperty targetProperty, TemplateBinding templateBinding)
    {
        var host = Context.HostComponent;
        var sourceProperty = host.GetProperty(templateBinding.Path);
        if (sourceProperty == null) return;

        component.SetTriggerValue(targetProperty, host.GetValue(sourceProperty), setter);

        void OnHostPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
        {
            if (e.Property == sourceProperty) component.SetTriggerValue(targetProperty, e.NewValue, setter);
        }

        host.PropertyChanged += OnHostPropertyChanged;
        _applied[setter] = (component, () =>
        {
            host.PropertyChanged -= OnHostPropertyChanged;
            component.ClearTriggerValue(targetProperty, setter);
        });
    }

    private void RemoveSetter(ISetter setter)
    {
        if (_applied.Remove(setter, out var applied))
            applied.Teardown();
    }
}
