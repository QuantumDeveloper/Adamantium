using System;

namespace Adamantium.UI.Core;

/// <summary>An object whose <see cref="AdamantiumProperty"/> values can be driven by animations.</summary>
public interface IAnimatableUIComponent
{
    /// <summary>
    /// Starts a <see cref="Media.Animation.DoubleAnimation"/> on a double property. The animation writes at
    /// <see cref="ValuePriority.Animation"/> (so it overrides Local/Binding while it runs) and holds its final value;
    /// <paramref name="completed"/> fires when it finishes. Re-calling for the same property restarts the animation.
    /// </summary>
    void BeginAnimation(AdamantiumProperty property, Media.Animation.DoubleAnimation animation, Action completed = null);

    /// <summary>
    /// Stops the animation on <paramref name="property"/> (whether running or holding its final value) and releases the
    /// animation-priority value, so the property reverts to its base value (Local/Binding/Style/Default). The
    /// animation's completion callback does NOT fire. No-op if nothing is animating the property.
    /// </summary>
    void CancelAnimation(AdamantiumProperty property);
}

/// <summary>
/// Base for everything the framework can animate (UI components via <see cref="FundamentalUIComponent"/>, and animatable
/// value objects such as <c>Transform</c>). Hosts the animation entry point so the property/value system in
/// <see cref="AdamantiumComponent"/> stays free of animation concerns.
/// </summary>
public class AnimatableUIComponent : AdamantiumComponent, IAnimatableUIComponent
{
    public static readonly AdamantiumProperty TransitionsProperty = AdamantiumProperty.Register(nameof(Transitions),
        typeof(Media.Animation.Transitions), typeof(AnimatableUIComponent));

    /// <summary>
    /// Implicit transitions for this element: when a watched property's base value changes (from code, binding, style
    /// or state), the displayed value animates from its current value to the new one instead of snapping. Lazily
    /// created on first access (markup populates it via Add), so elements without transitions - e.g. most Transforms -
    /// allocate nothing. (No change-tracking needed: transitions are passive, read on demand, with no attach/detach.)
    /// </summary>
    public Media.Animation.Transitions Transitions
    {
        get
        {
            var transitions = GetValue<Media.Animation.Transitions>(TransitionsProperty);
            if (transitions == null)
            {
                transitions = new Media.Animation.Transitions();
                SetValue(TransitionsProperty, transitions);
            }
            return transitions;
        }
        set => SetValue(TransitionsProperty, value);
    }

    /// <inheritdoc />
    public void BeginAnimation(AdamantiumProperty property, Media.Animation.DoubleAnimation animation, Action completed = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(animation);
        Media.Animation.AnimationManager.Begin(this, property, animation, completed);
    }

    /// <summary>Drives implicit transitions: a base-value change on a property with a matching transition becomes a
    /// smooth current-&gt;new animation instead of an instant set.</summary>
    protected override bool OnValueSet(AdamantiumProperty property, object oldEffectiveValue, object newValue, ValuePriority priority)
    {
        // Raw read (not the lazy getter) so an element that never declared transitions allocates nothing on a value set.
        var transitions = GetValue<Media.Animation.Transitions>(TransitionsProperty);
        if (transitions == null || transitions.Count == 0)
            return false;

        foreach (var transition in transitions)
        {
            if (transition.Property == property.Name)
            {
                return transition.TryApply(this, property, oldEffectiveValue, newValue);
            }
        }

        return false;
    }

    /// <inheritdoc />
    public void CancelAnimation(AdamantiumProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        Media.Animation.AnimationManager.Cancel(this, property);
        // Release the animation-priority value (running or held-final) so the property reverts to its base value.
        ClearValue(property, ValuePriority.Animation);
    }
}
