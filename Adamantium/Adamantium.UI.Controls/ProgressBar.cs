using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// Shows how far along a [<see cref="RangeBase.Minimum"/>, <see cref="RangeBase.Maximum"/>] operation is as a linear bar:
/// the control computes the filled <see cref="Percentage"/> and sizes the template's <c>PART_Indicator</c> fill to that
/// fraction of <c>PART_Track</c>. Mirrors WPF's ProgressBar. The circular gauge is <see cref="RingProgressBar"/>.
/// </summary>
public class ProgressBar : RangeBase
{
    private MeasurableUIComponent _indicator;   // PART_Indicator - the fill
    private MeasurableUIComponent _track;       // PART_Track - the full extent the fill is a fraction of

    public static readonly AdamantiumProperty IsIndeterminateProperty = AdamantiumProperty.Register(
        nameof(IsIndeterminate), typeof(bool), typeof(ProgressBar),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(ProgressBar),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    static ProgressBar()
    {
        // Progress convention: a 0..100 range (vs RangeBase's 0..1). A metadata default, NOT a constructor set - a set
        // writes Local priority, which outranks and permanently masks a {Binding}/Style/Trigger on Maximum. Re-use
        // RangeBase's OnMaximumChanged so the re-coercion still fires.
        MaximumProperty.OverrideMetadata(typeof(ProgressBar), new PropertyMetadata(100.0, OnMaximumChanged));
    }

    /// <summary>True while the operation's extent is unknown - the theme shows a moving/indefinite indicator instead of
    /// a value-driven fill (the determinate fill is not applied).</summary>
    public bool IsIndeterminate
    {
        get => GetValue<bool>(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Layout axis of the linear bar (ignored by the circular template).</summary>
    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The filled fraction, 0..1: (Value - Minimum) / (Maximum - Minimum); 0 when the range is empty.</summary>
    public double Percentage => Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0.0;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _indicator = GetTemplateChild("PART_Indicator") as MeasurableUIComponent;
        _track = GetTemplateChild("PART_Track") as MeasurableUIComponent;
        UpdateIndicator();
    }

    protected override void OnValueChanged(double oldValue, double newValue) => UpdateIndicator();

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The bar stretches only along its Orientation: clamp the CROSS axis to the content thickness (DesiredSize) BEFORE
        // arranging, so the template settles onto the real bar and ActualWidth/Height report the visible bar, not the whole
        // slot (a default-Stretch bar otherwise fills the slot's height with a 6px line centred inside it).
        finalSize = Orientation == Orientation.Horizontal
            ? new Size(finalSize.Width, Math.Min(finalSize.Height, DesiredSize.Height))
            : new Size(Math.Min(finalSize.Width, DesiredSize.Width), finalSize.Height);

        var size = base.ArrangeOverride(finalSize);
        UpdateIndicator();   // PART_Track is laid out now, so the fill's pixel width is finally known
        return size;
    }

    // Sizes the fill to Percentage of the track's main axis. Determinate-only - an indeterminate bar is left to the theme.
    private void UpdateIndicator()
    {
        if (_indicator == null || IsIndeterminate) return;

        var fraction = Math.Clamp(Percentage, 0.0, 1.0);

        if (Orientation == Orientation.Horizontal)
        {
            var full = _track?.ActualWidth ?? ActualWidth;
            if (full > 0) SetIfChanged(WidthProperty, fraction * full, _indicator.Width);
        }
        else
        {
            var full = _track?.ActualHeight ?? ActualHeight;
            if (full > 0) SetIfChanged(HeightProperty, fraction * full, _indicator.Height);
        }
    }

    // The fill size is set during arrange, which re-invalidates measure; only re-set it on a real change so the layout
    // settles instead of looping (NaN means "never set yet", so always set the first time).
    private void SetIfChanged(AdamantiumProperty property, double target, double current)
    {
        if (double.IsNaN(current) || Math.Abs(current - target) > 0.5)
            _indicator.SetValue(property, target);
    }
}
