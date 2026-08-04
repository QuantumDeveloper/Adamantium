using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// Runs its <see cref="TriggerBase.EnterActions"/> each time a routed <see cref="Event"/> fires on the trigger's host
/// (the WPF <c>EventTrigger</c> analog). Unlike a <see cref="PropertyTrigger"/> it has no lasting condition or setters -
/// it is a one-shot fire per event, with no exit edge. The canonical use is starting a looping animation when a
/// template element is <c>Loaded</c> (e.g. the loading-overlay shimmer sweeping a gradient forever).
/// </summary>
public class EventTrigger : TriggerBase
{
    /// <summary>The routed event whose firing runs the actions (e.g. <c>InputUIComponent.LoadedEvent</c>).</summary>
    public RoutedEvent Event { get; set; }

    /// <summary>Also receive the event after another handler marked it handled.</summary>
    public bool HandledEventsToo { get; set; }

    public override ITriggerActivator Apply(ITriggerExecutionContext context)
    {
        var activator = new EventTriggerActivator(context, this);
        activator.Activate();
        return activator;
    }
}

/// <summary>Subscribes the trigger's host to <see cref="EventTrigger.Event"/> and runs the trigger's EnterActions on
/// every fire. An EventTrigger only starts actions (never targets a part with a setter), so it is template-independent.</summary>
internal sealed class EventTriggerActivator : ITriggerActivator
{
    private readonly ITriggerExecutionContext _context;
    private readonly EventTrigger _trigger;
    private IObservableComponent _source;
    private bool _subscribed;

    public EventTriggerActivator(ITriggerExecutionContext context, EventTrigger trigger)
    {
        _context = context;
        _trigger = trigger;
    }

    public bool TargetsTemplateParts => false;

    public void Activate()
    {
        // Idempotent: Apply activates, and some hosts re-activate on a template swap - a second AddHandler would run the
        // actions twice per event.
        if (_subscribed || _trigger.Event == null) return;
        _source = _context.HostComponent as IObservableComponent;
        if (_source == null) return;
        _source.AddHandler(_trigger.Event, OnEvent, _trigger.HandledEventsToo);
        _subscribed = true;
    }

    public void Deactivate()
    {
        if (!_subscribed) return;
        _source.RemoveHandler(_trigger.Event, (RoutedEventHandler)OnEvent);
        _subscribed = false;
    }

    // An event trigger holds no state between firings - it runs its actions when the event arrives and forgets them - so
    // there is nothing for a detached host to suspend or a returning one to resume. NB an event action that starts a
    // LOOPING animation is therefore not covered here; nothing records that it is running.
    public void SuspendActions() { }

    public void ResumeActions() { }

    private void OnEvent(object sender, RoutedEventArgs e)
    {
        foreach (var action in _trigger.EnterActions)
            action.Invoke(_context);
    }
}
