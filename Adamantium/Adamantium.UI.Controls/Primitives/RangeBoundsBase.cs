using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// Everything a control over a [<see cref="Minimum"/>, <see cref="Maximum"/>] range needs EXCEPT what it selects inside
/// that range. <see cref="RangeBase"/> adds a single Value; <see cref="RangeSlider"/> adds a pair of bounds. Both need
/// the same bounds, the same step sizes, and the same rule that moving a bound re-checks whatever is selected - which is
/// what lives here, rather than being written twice.
/// </summary>
public abstract class RangeBoundsBase : Control
{
    public static readonly AdamantiumProperty MinimumProperty = AdamantiumProperty.Register(nameof(Minimum),
        typeof(double), typeof(RangeBoundsBase), new PropertyMetadata(0.0, OnMinimumChanged));

    public static readonly AdamantiumProperty MaximumProperty = AdamantiumProperty.Register(nameof(Maximum),
        typeof(double), typeof(RangeBoundsBase), new PropertyMetadata(1.0, OnMaximumChanged));

    public static readonly AdamantiumProperty SmallChangeProperty = AdamantiumProperty.Register(nameof(SmallChange),
        typeof(double), typeof(RangeBoundsBase), new PropertyMetadata(1.0));

    public static readonly AdamantiumProperty LargeChangeProperty = AdamantiumProperty.Register(nameof(LargeChange),
        typeof(double), typeof(RangeBoundsBase), new PropertyMetadata(10.0));

    /// <summary>Lower bound. Anything selected below this clamps up.</summary>
    public double Minimum
    {
        get => GetValue<double>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Upper bound. Kept &gt;= Minimum by coercing what is selected (the bounds themselves are not reordered).</summary>
    public double Maximum
    {
        get => GetValue<double>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
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

    // protected static so a subclass that wants a different default Minimum/Maximum (Slider/ProgressBar = 0..100,
    // ScrollBar = 0..0) can re-use it in OverrideMetadata - keeping the re-coercion + OnRangeBoundsChanged behaviour that
    // a fresh PropertyMetadata would otherwise drop. (Subclasses set the default via metadata, NOT a constructor set,
    // which would write Local priority and permanently mask a {Binding}/Style/Trigger on the property.)
    protected static void OnMinimumChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double) return;
        var range = (RangeBoundsBase)d;
        range.ReCoerceSelection();
        range.OnRangeBoundsChanged();
    }

    protected static void OnMaximumChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.NewValue is not double) return;
        var range = (RangeBoundsBase)d;
        range.ReCoerceSelection();
        range.OnRangeBoundsChanged();
    }

    /// <summary>Re-run the coercion of whatever this control selects - a bound just moved and may have left it outside.
    /// One value for a Slider, two for a RangeSlider.</summary>
    protected abstract void ReCoerceSelection();

    /// <summary>Minimum or Maximum changed. The selected fraction of the range shifts even when the selection itself is
    /// unchanged, so a fraction-driven visual (a Slider's accent fill) must recompute here - a value-changed callback
    /// alone misses a pure range rescale.</summary>
    protected virtual void OnRangeBoundsChanged()
    {
    }
}
