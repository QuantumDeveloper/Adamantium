namespace Adamantium.UI.Core.Media.Animation;

/// <summary>A <see cref="Transition"/> for <see cref="double"/> properties (Opacity, Width, Transform.TranslateX, ...).</summary>
public sealed class DoubleTransition : Transition
{
    internal override bool TryApply(AnimatableUIComponent target, AdamantiumProperty property, object oldValue, object newValue)
    {
        if (oldValue is not double from || newValue is not double to)
            return false;
        if (double.IsNaN(from) || double.IsNaN(to) || from == to)
            return false;   // first real value (from Auto/NaN) or a no-op: snap, don't animate

        target.BeginAnimation(property, new DoubleAnimation { From = from, To = to, Duration = Duration, Easing = Easing });
        return true;
    }
}
