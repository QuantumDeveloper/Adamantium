using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// Base for controls over a numeric value within a [<see cref="Minimum"/>, <see cref="Maximum"/>] range - ScrollBar
/// today, Slider/ProgressBar later. <see cref="Value"/> is always coerced into the range, and a Minimum/Maximum change
/// re-coerces it. Mirrors WPF's RangeBase.
/// </summary>
public abstract class RangeBase : Control
{
    public static readonly AdamantiumProperty MinimumProperty = AdamantiumProperty.Register(nameof(Minimum),
        typeof(double), typeof(RangeBase), new PropertyMetadata(0.0, OnMinimumChanged));

    public static readonly AdamantiumProperty MaximumProperty = AdamantiumProperty.Register(nameof(Maximum),
        typeof(double), typeof(RangeBase), new PropertyMetadata(1.0, OnMaximumChanged));

    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(double), typeof(RangeBase), new PropertyMetadata(0.0, OnValueChanged, CoerceValue));

    public static readonly AdamantiumProperty SmallChangeProperty = AdamantiumProperty.Register(nameof(SmallChange),
        typeof(double), typeof(RangeBase), new PropertyMetadata(1.0));

    public static readonly AdamantiumProperty LargeChangeProperty = AdamantiumProperty.Register(nameof(LargeChange),
        typeof(double), typeof(RangeBase), new PropertyMetadata(10.0));

    /// <summary>Lower bound. A value below this clamps up.</summary>
    public double Minimum
    {
        get => GetValue<double>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Upper bound. Kept &gt;= Minimum by coercion of Value (Minimum/Maximum themselves are not reordered).</summary>
    public double Maximum
    {
        get => GetValue<double>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Current value, always within [Minimum, Maximum].</summary>
    public double Value
    {
        get => GetValue<double>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Step for a small change (e.g. a scrollbar line button / arrow key).</summary>
    public double SmallChange
    {
        get => GetValue<double>(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    /// <summary>Step for a large change (e.g. a scrollbar page / PageUp-PageDown).</summary>
    public double LargeChange
    {
        get => GetValue<double>(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
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

    private static void OnMinimumChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.NewValue is double) ((RangeBase)d).ReCoerceValue();
    }

    private static void OnMaximumChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.NewValue is double) ((RangeBase)d).ReCoerceValue();
    }

    // A Minimum/Maximum change can pull Value out of range. Re-assign Value so the coercion clamps it again; if it was
    // already in range the coercion returns the same value and no ValueChanged fires (guarded below). Re-set at Value's
    // CURRENT priority, not the default Local: a Local re-coerce (Local=1) would outrank a {Binding} on Value (Binding=2)
    // and pin a data-bound slider at its coerced Minimum, because Minimum is applied (coercing the default 0 up to Min)
    // BEFORE the binding resolves - so the coerced default must not be promoted above the binding.
    private void ReCoerceValue() => SetValue(ValueProperty, Value, GetBaseValuePriority(ValueProperty));

    private static void OnValueChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        // Skip the construction-time callback (Unset sentinel) and no-op coercions.
        if (e.OldValue is not double oldValue || e.NewValue is not double newValue || oldValue == newValue) return;

        var range = (RangeBase)d;
        range.OnValueChanged(oldValue, newValue);
        range.ValueChanged?.Invoke(range, new ValueChangedEventArgs(oldValue, newValue));
    }

    protected virtual void OnValueChanged(double oldValue, double newValue)
    {
    }
}
