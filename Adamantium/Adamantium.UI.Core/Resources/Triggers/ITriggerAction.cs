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
/// An action that reaches a NAMED template part rather than the host itself (it animates one, it stops one). A trigger
/// can therefore depend on the template through its ACTIONS alone, with no setter naming a part - and such a trigger must
/// still be re-pointed when the template is rebuilt, or it is left acting on parts that were thrown away
/// (<see cref="ITriggerActivator.TargetsTemplateParts"/>).
/// </summary>
public interface ITargetedTriggerAction : ITriggerAction
{
    /// <summary>The named part this action acts on; empty means the trigger's host.</summary>
    string TargetName { get; }
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
    /// <param name="target">The component the action was actually INVOKED on, remembered by the activator. Re-resolving
    /// the name here would be wrong on a template swap: by teardown time it resolves to the NEW part, so the animation
    /// would be stopped on a part that never started one while the old part kept ticking forever.</param>
    void Undo(ITriggerExecutionContext context, IAdamantiumComponent target);
}
