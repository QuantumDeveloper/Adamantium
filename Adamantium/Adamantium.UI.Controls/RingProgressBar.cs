using System;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Circular progress gauge over a [<see cref="RangeBase.Minimum"/>, <see cref="RangeBase.Maximum"/>] range: a dim full
/// ring under an accent arc swept to the filled <see cref="Percentage"/>. Always square - it derives the missing side
/// from the one the consumer set, so sizing by only Width OR only Height still gives a round ring. Circle-specific knobs:
/// <see cref="RingThickness"/>, an optional centred percentage label (<see cref="ShowValueText"/>), the arc's
/// <see cref="StartPosition"/> (which clock position 0% sits at) and its sweep <see cref="Direction"/>. The linear
/// variant is <see cref="ProgressBar"/>.
/// </summary>
public class RingProgressBar : RangeBase
{
    private Ellipse _indicator;      // PART_Indicator - the accent arc swept to Percentage
    private Transform _startRotate;  // rotates the arc so its 0% end lands at StartPosition

    public static readonly AdamantiumProperty RingThicknessProperty = AdamantiumProperty.Register(nameof(RingThickness),
        typeof(double), typeof(RingProgressBar), new PropertyMetadata(5.0, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty ShowValueTextProperty = AdamantiumProperty.Register(nameof(ShowValueText),
        typeof(bool), typeof(RingProgressBar), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ValueTextProperty = AdamantiumProperty.Register(nameof(ValueText),
        typeof(string), typeof(RingProgressBar), new PropertyMetadata("0%"));

    public static readonly AdamantiumProperty StartPositionProperty = AdamantiumProperty.Register(nameof(StartPosition),
        typeof(RingStartPosition), typeof(RingProgressBar), new PropertyMetadata(RingStartPosition.Top, OnStartPositionChanged));

    public static readonly AdamantiumProperty DirectionProperty = AdamantiumProperty.Register(nameof(Direction),
        typeof(SweepDirection), typeof(RingProgressBar), new PropertyMetadata(SweepDirection.Clockwise, OnDirectionChanged));

    static RingProgressBar()
    {
        // Progress convention: 0..100 (vs RangeBase's 0..1). A metadata default, NOT a constructor set - a set writes Local
        // priority, which outranks and permanently masks a {Binding}/Style/Trigger on Maximum. Re-use RangeBase's callback.
        MaximumProperty.OverrideMetadata(typeof(RingProgressBar), new PropertyMetadata(100.0, OnMaximumChanged));
    }

    /// <summary>Stroke width of both the track ring and the accent arc, in DIPs.</summary>
    public double RingThickness
    {
        get => GetValue<double>(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    /// <summary>Show the filled percentage centred inside the ring.</summary>
    public bool ShowValueText
    {
        get => GetValue<bool>(ShowValueTextProperty);
        set => SetValue(ShowValueTextProperty, value);
    }

    /// <summary>The filled percentage as text (e.g. "42%") - the template binds the centre label to it. Read-only output.</summary>
    public string ValueText
    {
        get => GetValue<string>(ValueTextProperty);
        private set => SetValue(ValueTextProperty, value);
    }

    /// <summary>Clock position of the arc's 0% end.</summary>
    public RingStartPosition StartPosition
    {
        get => GetValue<RingStartPosition>(StartPositionProperty);
        set => SetValue(StartPositionProperty, value);
    }

    /// <summary>Which way the arc grows from <see cref="StartPosition"/> as the value rises.</summary>
    public SweepDirection Direction
    {
        get => GetValue<SweepDirection>(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Filled fraction, 0..1: (Value - Minimum) / (Maximum - Minimum); 0 when the range is empty.</summary>
    public double Percentage => Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0.0;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _indicator = GetTemplateChild("PART_Indicator") as Ellipse;
        if (_indicator != null)
        {
            // The Ellipse arc always sweeps from its own angle 0 (at 3 o'clock) in ONE direction (its StopAngle is a
            // sweep span, clamped >= 0 - it can't render a negative sweep). So the arc transform does two things: a
            // rotation puts the 0% end at StartPosition, and a vertical flip (ScaleY = -1) reverses the winding for the
            // opposite Direction - the flip keeps the on-axis 3 o'clock start fixed, so the same rotation still applies.
            _startRotate = new Transform();
            _indicator.RenderTransform = _startRotate;
        }
        ApplyArcTransform();
        UpdateIndicator();
    }

    protected override void OnValueChanged(double oldValue, double newValue) => UpdateIndicator();

    // A pure range rescale (Minimum/Maximum) shifts Percentage even when Value is unchanged - OnValueChanged alone misses it.
    protected override void OnLimitsChanged() => UpdateIndicator();

    private static void OnStartPositionChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is RingProgressBar r) r.ApplyArcTransform();
    }

    private static void OnDirectionChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is RingProgressBar r) r.ApplyArcTransform();
    }

    // Rotate the arc's 0% end (natively 3 o'clock) to StartPosition, and flip it vertically for a clockwise sweep - the
    // Ellipse's native sweep runs counter-clockwise on screen, so Clockwise is the mirrored one.
    private void ApplyArcTransform()
    {
        if (_startRotate == null) return;
        _startRotate.RotationAngle = StartPosition switch
        {
            RingStartPosition.Right => 0.0,
            RingStartPosition.Bottom => 90.0,
            RingStartPosition.Left => 180.0,
            _ => 270.0,   // Top
        };
        _startRotate.ScaleY = Direction == SweepDirection.Clockwise ? -1.0 : 1.0;
    }

    // Round to a square: the side is whichever of Width/Height the consumer set (both -> the smaller; neither -> the MinWidth
    // default), reported on BOTH axes and used to measure the template. A Shape sizes its render rect to the space it is
    // measured with, so measuring against the square keeps the ring's Ellipses in step with the control instead of painting
    // to the (possibly unbounded) available space.
    protected override Size MeasureOverride(Size availableSize)
    {
        var w = Width;
        var h = Height;
        double side;
        if (!double.IsNaN(w) && !double.IsNaN(h)) side = Math.Min(w, h);
        else if (!double.IsNaN(w)) side = w;
        else if (!double.IsNaN(h)) side = h;
        else side = MinWidth;

        var square = new Size(side, side);
        base.MeasureOverride(square);
        return square;
    }

    // Sweep the arc to Percentage and refresh the centre label. StopAngle is a SWEEP span from StartAngle, so the arc is
    // always [start 0, sweep fraction*360]; StartPosition (rotation) and Direction (flip) are handled by the transform.
    // Size-independent, so - unlike a linear fill - this needs no arrange.
    private void UpdateIndicator()
    {
        var fraction = Math.Clamp(Percentage, 0.0, 1.0);
        ValueText = $"{Math.Round(fraction * 100)}%";

        if (_indicator == null) return;
        _indicator.StartAngle = 0;
        _indicator.StopAngle = fraction * 360.0;
    }
}
