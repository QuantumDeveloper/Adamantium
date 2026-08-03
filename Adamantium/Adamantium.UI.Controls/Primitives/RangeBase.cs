using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// A control over a SINGLE value within a [<see cref="RangeLimitsBase.Minimum"/>, <see cref="RangeLimitsBase.Maximum"/>]
/// range - ScrollBar, Slider, ProgressBar. <see cref="Value"/> is always coerced into the range, and a Minimum/Maximum
/// change re-coerces it. Mirrors WPF's RangeBase; the bounds themselves live one level up, shared with the controls that
/// select a SPAN rather than a point (see <see cref="RangeSlider"/>).
/// </summary>
public abstract class RangeBase : RangeLimitsBase
{
    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(double), typeof(RangeBase), new PropertyMetadata(0.0, OnValueChanged, CoerceValue));

    /// <summary>Current value, always within [Minimum, Maximum].</summary>
    public double Value
    {
        get => GetValue<double>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public event EventHandler<ValueChangedEventArgs> ValueChanged;

    private static object CoerceValue(AdamantiumComponent d, object baseValue)
    {
        // Construction-time coercion can hand us the "Unset" sentinel rather than a double - leave it untouched.
        if (baseValue is not double value) return baseValue;
        var range = (RangeBase)d;
        if (value < range.Minimum) value = range.Minimum;
        if (value > range.Maximum) value = range.Maximum;
        return value;
    }

    // A Minimum/Maximum change can pull Value out of range - or free a value that had to be clamped by the previous
    // bounds. Re-coercion re-maps the REQUEST at its own priority, so neither happens at the cost of the other: a
    // data-bound slider is not pinned to a coerced default, and a value clamped earlier is restored when it fits again.
    protected override void ReCoerceSelection() => CoerceValue(ValueProperty);

    private static void OnValueChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double newValue) return;

        // The FIRST value has no previous one - the old value is the "unset" sentinel, not a number. Treating that as
        // "nothing changed" is what left a control showing its default for good: a value arriving from a binding after
        // the template was applied never reached the visual (a RingProgressBar bound to 40 sat at 0%). Take the default
        // as the previous value instead, and only then decide whether anything actually moved.
        var oldValue = e.OldValue as double? ?? (double)ValueProperty.GetDefaultMetadata(d.GetType()).DefaultValue;
        if (oldValue == newValue) return;

        var range = (RangeBase)d;
        range.OnValueChanged(oldValue, newValue);
        range.ValueChanged?.Invoke(range, new ValueChangedEventArgs(oldValue, newValue));
    }

    protected virtual void OnValueChanged(double oldValue, double newValue)
    {
    }
}
