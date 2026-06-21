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
