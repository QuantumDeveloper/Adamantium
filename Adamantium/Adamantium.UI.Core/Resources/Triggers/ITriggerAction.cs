namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// An action a trigger runs on its enter (condition met) or exit (condition lost) edge - e.g. start an animation.
/// The WPF <c>TriggerAction</c> analog. Resolves its target through the trigger's execution context, so a template
/// trigger reaches named parts and a style/logical trigger acts on the host.
/// </summary>
public interface ITriggerAction
{
    void Invoke(ITriggerExecutionContext context);
}

/// <summary>
/// An enter-action that leaves LASTING state behind - a running (possibly looping) animation. A trigger is normally
/// undone by its exit edge, but an activator can also be torn down while its condition still HOLDS: a theme or template
/// swap deactivates it outright, and then the ExitActions never fire. Whatever the enter-action started would keep
/// ticking against a part/brush nobody shows any more, so the activator asks it to undo itself instead
/// (<see cref="TriggerActivatorBase.TearDown"/>).
/// </summary>
public interface IUndoableTriggerAction : ITriggerAction
{
    void Undo(ITriggerExecutionContext context);
}
