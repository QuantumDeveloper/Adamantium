using System;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// A declarative, CSS-style implicit transition: watches one property and, when its base value changes, smoothly
/// animates the displayed value from the current value to the new one instead of snapping. Declared in
/// <see cref="AnimatableUIComponent.Transitions"/>; any change source (code, binding, style, state) triggers it.
/// </summary>
public abstract class Transition
{
    /// <summary>The NAME of the property this transition animates (resolved against the element, like Setter.Property).
    /// A string so it is set directly from markup (<c>Property="Width"</c>); the engine supplies the resolved property.</summary>
    public string Property { get; set; }

    /// <summary>How long the transition takes.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Optional easing; linear when null.</summary>
    public IEasingFunction Easing { get; set; }

    /// <summary>
    /// Starts the transition for a value change (<paramref name="property"/> is the resolved property that changed).
    /// Returns false when it does not apply to these values (wrong type, no change, or a non-finite endpoint such as
    /// the first value coming from Auto/NaN) so the change is applied instantly.
    /// </summary>
    internal abstract bool TryApply(AnimatableUIComponent target, AdamantiumProperty property, object oldValue, object newValue);
}
