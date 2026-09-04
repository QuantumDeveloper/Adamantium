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
public abstract class TriggerActivatorBase : ITriggerActivator
{
    protected readonly ITriggerExecutionContext Context;
    private readonly TriggerBase _trigger;

    // What each setter currently has applied: the EXACT part it was pushed onto plus how to undo it (clear the trigger
    // value, drop any {ThemeResource}/{TemplateBinding} subscription). Tracking the real target - rather than re-asking
    // FindTarget at teardown - keeps cleanup correct across a runtime template swap, where FindTarget would by then
    // return the NEW part and leave the old one dirty and still subscribed (a leak).
    private readonly Dictionary<ISetter, (IAdamantiumComponent Target, Action Teardown)> _applied = new();
    private bool _conditionMet;

    // TEMP (leak hunt): activators made and torn down. An activator holds its CONTEXT, and a template trigger's context
    // holds the whole TemplateResult - so one that is never deactivated keeps a discarded template alive. Counted the
    // same way the brush links were, because that is the instrument that has actually worked today.
    public static long Made, TornDown;

    protected TriggerActivatorBase(ITriggerExecutionContext context, TriggerBase trigger)
    {
        Context = context;
        _trigger = trigger;
        System.Threading.Interlocked.Increment(ref Made);

        TargetsTemplateParts = ReachesTemplateParts(trigger);
    }

    // Does this trigger reach into the TEMPLATE - through a setter that names a part, OR through an ACTION that does?
    //
    // Actions count, and forgetting them was a real bug: a trigger whose only reach is a RunAnimationAction (a loading
    // indicator's spin/pulse lives entirely in its enter/exit actions - it has no setters at all) was classified as
    // template-INDEPENDENT, so a template rebuild never re-pointed it. The animation then kept running on the parts of the
    // discarded template while the rendered ones - the ones on screen - sat frozen. And a control gets re-templated
    // routinely: a class-selected look means both the base style and the class style write Template.
    private static bool ReachesTemplateParts(TriggerBase trigger)
    {
        // A trigger that READS a part reaches the template just as surely as one that writes to it: the part it listens
        // to is discarded by a swap, and an activator still subscribed to the old one hears nothing ever again.
        if (trigger is PropertyTrigger { SourceName.Length: > 0 }) return true;

        if (trigger.Setters != null)
            foreach (var setter in trigger.Setters)
                if (!string.IsNullOrEmpty(setter.TargetName))
                    return true;

        return NamesAPart(trigger.EnterActions) || NamesAPart(trigger.ExitActions);
    }

    private static bool NamesAPart(List<ITriggerAction> actions)
    {
        if (actions == null) return false;
        foreach (var action in actions)
            if (action is ITargetedTriggerAction { TargetName: { Length: > 0 } })
                return true;
        return false;
    }

    // A setter or action that names a part reaches the template; one that names nothing touches the host itself. Fixed for
    // the trigger's lifetime - neither its setters nor its actions change - so it is computed once.
    public bool TargetsTemplateParts { get; }

    public abstract void Activate();

    public abstract void Deactivate();

    /// <summary>Subclasses call this on activation and whenever a watched property changes, passing whether the
    /// condition (ALL conditions, for a MultiTrigger) now holds. Setters apply/remove idempotently for as long as it
    /// holds; Enter/Exit actions fire only on the EDGE - when the condition crosses, not on every change.</summary>
    protected void ApplyState(bool conditionMet)
    {
        TriggerProbe.Note(_trigger, Context?.HostComponent, conditionMet, _conditionMet);

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
        System.Threading.Interlocked.Increment(ref TornDown);

        // Snapshot and clear state BEFORE running teardowns: a teardown clears a trigger value, whose property-changed
        // callback can re-enter this activator (RemoveSetter/ApplySetter) and mutate _applied - iterating it live throws
        // "Collection was modified". This is what broke a runtime theme swap on the first triggered control it hit.
        (IAdamantiumComponent Target, Action Teardown)[] teardowns = [.. _applied.Values];
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
        var invoked = _actionTargets;
        _actionTargets = null;
        if (!wasConditionMet) return;
        UndoEnterActions(invoked);
    }

    // Undo whatever the ENTER actions left running, on the parts they were actually invoked against.
    private void UndoEnterActions(Dictionary<ITriggerAction, IAdamantiumComponent> invoked)
    {
        if (_trigger.EnterActions == null) return;

        foreach (var action in _trigger.EnterActions)
            if (action is IUndoableTriggerAction undoable)
                undoable.Undo(Context, invoked != null && invoked.TryGetValue(action, out var t) ? t : null);
    }

    private bool _suspended;

    /// <summary>See <see cref="ITriggerActivator.SuspendActions"/>. The setters and the condition stay exactly as they
    /// are - only what is RUNNING is stopped, so nothing has to be re-evaluated when the host comes back.</summary>
    public void SuspendActions()
    {
        if (_suspended || !_conditionMet) return;

        _suspended = true;
        UndoEnterActions(_actionTargets);   // keep the targets: Resume re-invokes and overwrites them
    }

    /// <summary>See <see cref="ITriggerActivator.ResumeActions"/>.</summary>
    public void ResumeActions()
    {
        if (!_suspended) return;

        _suspended = false;
        if (_conditionMet && _trigger.EnterActions != null) InvokeActions(_trigger.EnterActions);
    }

    // What each invoked action actually acted ON. Resolved at INVOKE time and kept, for the same reason the setters' targets
    // are (see _applied): by teardown the template may have been rebuilt, and re-resolving the name would hand back the NEW
    // part - so the action would be undone on something it never touched, and whatever it left running on the OLD part
    // (a looping animation) would tick on forever.
    private Dictionary<ITriggerAction, IAdamantiumComponent> _actionTargets;

    private void InvokeActions(IEnumerable<ITriggerAction> actions)
    {
        foreach (var action in actions)
        {
            if (action is ITargetedTriggerAction targeted)
            {
                _actionTargets ??= new Dictionary<ITriggerAction, IAdamantiumComponent>();
                _actionTargets[action] = Context.FindTarget(targeted.TargetName);
            }
            action.Invoke(Context);
        }
    }

    // A trigger setter's Value is the same family of markers a style setter resolves (see Setter.Apply): a
    // {ResourceReference} (one-shot palette lookup), a {ThemeResource} (live link to the active theme's accent/focus),
    // or a {TemplateBinding} (live link to a property of the templated control - this is what lets ONE template serve
    // both the base and Accent button). A plain string is parsed.
    private void ApplySetter(ISetter setter)
    {
        // A setter's target need not be a UI ELEMENT. A named Aura or Shadow declared inside a template is an
        // AdamantiumComponent with properties like anything else, and switching one on is exactly what a state trigger
        // is for - Aura.IsEnabled says so in its own summary. This resolved as IFundamentalUIComponent, so the cast
        // produced null for such a target and the setter did nothing, SILENTLY: the part was found by name, the trigger
        // fired, and nothing happened.
        //
        // Everything a plain-value setter needs - GetProperty, SetTriggerValue, ClearTriggerValue - is on
        // IAdamantiumComponent. Only the markers that link to a POSITION IN THE TREE need more than that (a tree-scoped
        // resource lookup, a ThemeResource, a relative binding), and each of those still asks for an element below.
        var target = Context.FindTarget(setter.TargetName);
        var component = target as IAdamantiumComponent;
        var element = target as IFundamentalUIComponent;

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
                // trigger fires later, once live).
                var theme = Context.Theme ?? UIAppContext.Current?.ThemeManager?.CurrentTheme;
                // Tree-scoped from the target when there IS a tree position, so a Local resource is reachable; by key
                // alone for a non-element target (an Aura, a Shadow), which has nowhere to be scoped from.
                object resource = null;
                var found = theme != null && (element != null
                    ? theme.TryGetResource(element, resourceReference.Name, out resource)
                    : theme.TryGetResource(resourceReference.Name, out resource));
                if (found) component.SetTriggerValue(prop, resource, setter);
                _applied[setter] = (component, () => component.ClearTriggerValue(prop, setter));
                break;

            // A live link to the ACTIVE THEME's accent/focus, which is established against an element. A non-element
            // target cannot hold one; {ObservableResource} is the marker that reaches those (its Apply takes any
            // IAdamantiumComponent), which is what a named Aura's colour uses.
            case ThemeResource themeResource when element != null:
                themeResource.Apply(element, setter.Property, ValuePriority.Trigger, setter);
                _applied[setter] = (component, () => ThemeResource.Remove(element, setter.Property, ValuePriority.Trigger, setter));
                break;

            case ObservableResource observableResource:
                observableResource.Apply(component, setter.Property, ValuePriority.Trigger, setter);
                _applied[setter] = (component, () => ObservableResource.Remove(component, setter.Property, ValuePriority.Trigger, setter));
                break;

            case TemplateBinding templateBinding when element != null:
                ApplyTemplateBinding(setter, element, prop, templateBinding);
                break;

            // {Ancestor}/{Self} as a part-targeting trigger value: wire a live binding at Trigger priority, torn down on
            // exit. NOTE: unlike the per-token stack above, two triggers writing the SAME part property via a relative
            // binding don't stack (removal clears the whole binding slot) - fine for the normal single-trigger case.
            // Both walk the TREE to find their source, so both need a target that is in one.
            case Ancestor ancestor when element != null:
                ancestor.Apply(element, setter.Property, ValuePriority.Trigger);
                _applied[setter] = (component, () => element.RemoveBinding(setter.Property));
                break;

            case Self self when element != null:
                self.Apply(element, setter.Property, ValuePriority.Trigger);
                _applied[setter] = (component, () => element.RemoveBinding(setter.Property));
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
        var had = _applied.Remove(setter, out var applied);
        var target = had ? applied.Target : null;
        if (had) applied.Teardown();

        TriggerProbe.NoteRemoval(_trigger, Context?.HostComponent, setter, had, target);
    }
}
