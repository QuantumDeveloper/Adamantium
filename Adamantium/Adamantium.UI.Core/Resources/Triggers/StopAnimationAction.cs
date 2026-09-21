using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// A trigger action that STOPS every animation running on its target - the trigger's host by default, or a named
/// element/part via <see cref="TargetName"/>. The WPF <c>StopStoryboard</c> analog. Pairs with
/// <see cref="RunAnimationAction"/> in a trigger's ExitActions to end a looping animation when the condition drops -
/// e.g. stop a loading card's pulse the moment it leaves the pending set (its real item has loaded).
/// </summary>
public class StopAnimationAction : ITriggerAction
{
    /// <summary>Name of the element/part whose animations to stop; empty means the trigger's host component.</summary>
    public string TargetName { get; set; }

    public void Invoke(ITriggerExecutionContext context)
    {
        // Release this HOST's claim (RunAnimationAction took it): a target shared by several hosts - a keyed theme brush
        // pulsed by every loading list - stops only once the last of them lets go. A single-host target stops right here,
        // as before.
        if (context.FindTarget(TargetName) is AdamantiumComponent target)
            AnimationManager.Release(target, context.HostComponent);
    }
}
