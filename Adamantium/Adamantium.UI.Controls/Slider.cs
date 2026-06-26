using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Picks a value from a continuous [<see cref="RangeBase.Minimum"/>, <see cref="RangeBase.Maximum"/>] range by
/// dragging a thumb along a track (or clicking the track to page, or arrow/page keys). Reuses the <see cref="Track"/>
/// primitive for thumb sizing/positioning and pixel-to-value mapping, so this class only owns the interaction; the
/// track, thumb and (optional) ticks are the theme template. Mirrors WPF's Slider, minus the IsMoveToPointEnabled knob.
/// </summary>
public class Slider : RangeBase
{
    private Track _track;
    private MeasurableUIComponent _selectionRange;   // PART_SelectionRange - the accent fill (start..thumb)
    private double _dragStartValue;

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(Slider),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty TickFrequencyProperty = AdamantiumProperty.Register(nameof(TickFrequency),
        typeof(double), typeof(Slider), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty IsSnapToTickEnabledProperty = AdamantiumProperty.Register(
        nameof(IsSnapToTickEnabled), typeof(bool), typeof(Slider), new PropertyMetadata(false));

    static Slider()
    {
        Keyboard.KeyDownEvent.RegisterClassHandler<Slider>(new KeyEventHandler(KeyDownClassHandler));
    }

    public Slider()
    {
        Maximum = 100;          // slider convention (RangeBase defaults to a 0..1 range)
        LargeChange = 10;
        SmallChange = 1;
        Focusable = true;
    }

    /// <summary>Horizontal (default) or vertical track.</summary>
    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Spacing between snap ticks in value units; 0 = no ticks. Only snaps when <see cref="IsSnapToTickEnabled"/>.</summary>
    public double TickFrequency
    {
        get => GetValue<double>(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    /// <summary>When true, drag/page/keys land the value on the nearest <see cref="TickFrequency"/> multiple.</summary>
    public bool IsSnapToTickEnabled
    {
        get => GetValue<bool>(IsSnapToTickEnabledProperty);
        set => SetValue(IsSnapToTickEnabledProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _track = GetTemplateChild("PART_Track") as Track;
        _selectionRange = GetTemplateChild("PART_SelectionRange") as MeasurableUIComponent;
        if (_track != null)
        {
            // The parts now come from the template, so a malformed one may omit them - guard each.
            if (_track.Thumb != null)
            {
                _track.Thumb.DragStarted += OnThumbDragStarted;
                _track.Thumb.DragDelta += OnThumbDragDelta;
            }
            if (_track.IncreaseRepeatButton != null) _track.IncreaseRepeatButton.Click += OnIncrease;
            if (_track.DecreaseRepeatButton != null) _track.DecreaseRepeatButton.Click += OnDecrease;
        }
        UpdateFill();
    }

    private void DetachParts()
    {
        if (_track == null) return;
        if (_track.Thumb != null)
        {
            _track.Thumb.DragStarted -= OnThumbDragStarted;
            _track.Thumb.DragDelta -= OnThumbDragDelta;
        }
        if (_track.IncreaseRepeatButton != null) _track.IncreaseRepeatButton.Click -= OnIncrease;
        if (_track.DecreaseRepeatButton != null) _track.DecreaseRepeatButton.Click -= OnDecrease;
        _track = null;
    }

    // Thumb drag: e.Change is the CUMULATIVE pointer movement since the press (in the Track's stable space), so map it
    // from the value captured at drag start rather than accumulating per-event deltas (which drift).
    private void OnThumbDragStarted(object sender, DragStartedEventArgs e) => _dragStartValue = Value;

    private void OnThumbDragDelta(object sender, DragEventArgs e)
    {
        if (_track == null) return;
        Value = SnapToTick(_dragStartValue + _track.ValueFromDistance(e.Change.X, e.Change.Y));
    }

    private void OnIncrease(object sender, RoutedEventArgs e) => Value = SnapToTick(Value + LargeChange);
    private void OnDecrease(object sender, RoutedEventArgs e) => Value = SnapToTick(Value - LargeChange);

    // The accent fill (PART_SelectionRange) is driven exactly like ProgressBar's PART_Indicator: project the value
    // fraction onto its WIDTH on every Value change (and once more after layout, when the track's pixel length is known).
    // Setting Width carries AffectsRender, so it repaints live - a part sized only by the Track's arrange would not.
    protected override void OnValueChanged(double oldValue, double newValue) => UpdateFill();

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Honest cross-axis: a horizontal slider is only as tall as its content (rail + thumb), NOT the whole slot - else
        // its ClipRectangle swallows clicks/hovers across the slot and blocks other controls' input. Stretch stays on the
        // MAIN axis. (Mirror of ProgressBar.)
        finalSize = Orientation == Orientation.Horizontal
            ? new Size(finalSize.Width, Math.Min(finalSize.Height, DesiredSize.Height))
            : new Size(Math.Min(finalSize.Width, DesiredSize.Width), finalSize.Height);

        var size = base.ArrangeOverride(finalSize);
        UpdateFill();   // PART_Track is laid out now, so the fill's 100% length is finally known
        return size;
    }

    private void UpdateFill()
    {
        if (_selectionRange == null || _track == null) return;

        var range = Maximum - Minimum;
        var fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0.0, 1.0) : 0.0;

        if (Orientation == Orientation.Horizontal)
        {
            var full = _track.ActualWidth;
            if (full > 0) SetIfChanged(WidthProperty, fraction * full, _selectionRange.Width);
        }
        else
        {
            var full = _track.ActualHeight;
            if (full > 0) SetIfChanged(HeightProperty, fraction * full, _selectionRange.Height);
        }
    }

    // Set during arrange, which re-invalidates measure; only re-set on a real change so layout settles instead of looping
    // (NaN = never set yet -> always set the first time).
    private void SetIfChanged(AdamantiumProperty property, double target, double current)
    {
        if (double.IsNaN(current) || Math.Abs(current - target) > 0.5)
            _selectionRange.SetValue(property, target);
    }

    // Lands a value on the nearest tick when snapping is on; otherwise returns it unchanged (RangeBase clamps to range).
    private double SnapToTick(double value)
    {
        if (!IsSnapToTickEnabled || TickFrequency <= 0) return value;
        var snapped = Minimum + Math.Round((value - Minimum) / TickFrequency) * TickFrequency;
        return snapped;
    }

    private static void KeyDownClassHandler(object sender, KeyEventArgs e)
    {
        if (sender is Slider slider) slider.OnKeyDownInternal(e);
    }

    private void OnKeyDownInternal(KeyEventArgs e)
    {
        if (!IsEnabled) return;

        // Reversed so Up/Right increase and Down/Left decrease for BOTH orientations (a vertical slider's top is its max).
        switch (e.Key)
        {
            case Key.RightArrow or Key.UpArrow: Value = SnapToTick(Value + SmallChange); e.Handled = true; break;
            case Key.LeftArrow or Key.DownArrow: Value = SnapToTick(Value - SmallChange); e.Handled = true; break;
            case Key.PageUp: Value = SnapToTick(Value + LargeChange); e.Handled = true; break;
            case Key.PageDown: Value = SnapToTick(Value - LargeChange); e.Handled = true; break;
            case Key.Home: Value = Minimum; e.Handled = true; break;
            case Key.End: Value = Maximum; e.Handled = true; break;
        }
    }
}
