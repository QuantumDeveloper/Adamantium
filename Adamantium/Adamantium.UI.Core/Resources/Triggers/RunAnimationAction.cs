namespace Adamantium.UI.Core.Resources.Triggers;

/// <summary>
/// A trigger action that starts a keyframe <see cref="Media.Animation.Animation"/> on the trigger's target - the host
/// component by default, or a named element/part via <see cref="TargetName"/> (a template trigger reaches its parts).
/// The WPF <c>BeginStoryboard</c> analog.
/// </summary>
public class RunAnimationAction : ITriggerAction
{
    [Content]
    public Media.Animation.Animation Animation { get; set; }

    /// <summary>Name of the element/part to animate; empty means the trigger's host component.</summary>
    public string TargetName { get; set; }

    public void Invoke(ITriggerExecutionContext context)
    {
        if (Animation == null)
            return;
        if (context.FindTarget(TargetName) is AnimatableUIComponent target)
            Animation.Apply(target);
    }
}
