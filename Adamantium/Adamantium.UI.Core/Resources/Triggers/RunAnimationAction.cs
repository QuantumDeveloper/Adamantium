using System.Collections.Generic;

namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// A trigger action that starts a keyframe <see cref="Media.Animation.Animation"/> on the trigger's target - the host
/// component by default, or a named element/part via <see cref="TargetName"/> (a template trigger reaches its parts).
/// The WPF <c>BeginStoryboard</c> analog.
/// </summary>
public class RunAnimationAction : IUndoableTriggerAction, ITargetedTriggerAction
{
    [Content]
    public Media.Animation.Animation Animation { get; set; }

    /// <summary>Name of the element/part to animate; empty means the trigger's host component.</summary>
    public string TargetName { get; set; }

    // A re-templated trigger (a theme swap rebuilds a BusyIndicator's parts) tears the OLD target's animation down and
    // starts a fresh one on the NEW part - which would snap a spinner back to its start. Carry the phase across, keyed by
    // (host, this action) so distinct hosts and distinct animations on one host don't collide. Written in Undo, consumed by
    // the Deactivate->Activate that immediately follows (see ReevaluateTriggersForTemplateChange).
    private static readonly Dictionary<(IFundamentalUIComponent Host, RunAnimationAction Action), double> ResumePhase = new();

    public void Invoke(ITriggerExecutionContext context)
    {
        if (Animation == null) return;

        // Any AdamantiumComponent, not only an AnimatableUIComponent: a template can x:Name a NON-visual component (a
        // GradientStop inside a brush) and animate its double property (Offset) - e.g. a looping shimmer sweep. SetValue
        // and the keyframe track resolution both live on AdamantiumComponent.
        var target = context.FindTarget(TargetName);
        if (target is not AdamantiumComponent animTarget) return;

        // An enter action runs ONCE, as its condition becomes true - which, for a view built off the loop thread, is
        // while it is still being built. It cannot simply be skipped (nothing would ever ask again: the resume path only
        // re-runs what a DETACH suspended) and it cannot run here either, because the claim below and the phase above
        // live in tables the loop thread owns. So the whole action waits for the element to go up.
        if (Media.Animation.AnimationManager.DeferIfOutOfTree(
                animTarget, context.HostComponent as AdamantiumComponent, () => Start(context, animTarget))) return;

        Start(context, animTarget);
    }

    private void Start(ITriggerExecutionContext context, AdamantiumComponent animTarget)
    {
        var resume = ResumePhase.Remove((context.HostComponent, this), out var phase) ? phase : 0;

        // Claim the target for this trigger's HOST before starting: a SHARED target (a keyed theme brush every loading
        // list pulses) must keep running until the last host stops it (see AnimationManager.Retain/Release).
        Media.Animation.AnimationManager.Retain(animTarget, context.HostComponent);
        Animation.Apply(animTarget, resumeElapsed: resume);
    }

    /// <summary>The trigger is going away while it still held (a theme/template swap): drop this host's claim, which
    /// stops the animation unless another host still wants the target. Without it a LOOPING animation - a loading pulse -
    /// would tick forever against the discarded theme brush, one orphan per swap.</summary>
    public void Undo(ITriggerExecutionContext context, IAdamantiumComponent target)
    {
        // The target the action was STARTED on (the activator kept it), not a fresh FindTarget: on a template rebuild the
        // name now resolves to the NEW part, so releasing that would leave the animation running on the discarded one.
        if (target is not AdamantiumComponent animTarget) return;

        // Remember where it was so the successor (a re-template re-Invokes right after) resumes the phase. Only when it was
        // actually running (GetElapsed non-null), so a not-running teardown leaves nothing stale behind.
        var elapsed = Media.Animation.AnimationManager.GetElapsed(animTarget);
        if (elapsed.HasValue) ResumePhase[(context.HostComponent, this)] = elapsed.Value;

        Media.Animation.AnimationManager.Release(animTarget, context.HostComponent);
    }
}
