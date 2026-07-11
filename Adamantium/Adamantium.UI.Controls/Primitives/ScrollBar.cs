using System;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// A scrollbar over a [Minimum, Maximum] range, vertical or horizontal. The thumb is dragged for continuous scrolling;
/// the trough either side of it pages (LargeChange, auto-repeating); optional line buttons (PART_LineUpButton /
/// PART_LineDownButton, if the template supplies them) step by SmallChange. The default theme is a clean thumb+trough
/// (no arrow buttons), one template for both orientations via the Track's Orientation. Mirrors WPF's ScrollBar.
/// </summary>
public class ScrollBar : RangeBase
{
    // Conventional fixed cross-axis thickness applied in code so a standalone scrollbar has an intrinsic width/height
    // regardless of theme (the long axis stretches to its container).
    private const double DefaultThickness = 12.0;

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(ScrollBar),
        new PropertyMetadata(Orientation.Vertical, PropertyMetadataOptions.AffectsMeasure, OnOrientationChanged));

    public static readonly AdamantiumProperty ViewportSizeProperty = AdamantiumProperty.Register(nameof(ViewportSize),
        typeof(double), typeof(ScrollBar), new PropertyMetadata(0.0));

    private Track _track;
    private RepeatButton _lineUpButton;
    private RepeatButton _lineDownButton;
    private double _dragStartValue;

    static ScrollBar()
    {
        // Nothing to scroll by default: Maximum == Minimum (range 0) makes the Track fill the whole trough with an inert
        // thumb (density 0 -> drag/page are no-ops). A real Maximum + ViewportSize (set directly or by a ScrollViewer)
        // turns it into a proportional, draggable thumb. The default 0 is a metadata default (NOT a constructor set,
        // which writes Local priority and masks the {Binding}/direct Maximum a ScrollViewer applies). Re-use RangeBase's
        // OnMaximumChanged so the re-coercion still fires.
        MaximumProperty.OverrideMetadata(typeof(ScrollBar), new PropertyMetadata(0.0, OnMaximumChanged));
    }

    public ScrollBar()
    {
        ApplyOrientation();
    }

    /// <summary>Vertical (default) or Horizontal. Flips the Track layout and the control's intrinsic thickness axis.</summary>
    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The visible portion of the scrolled content, in range units. Drives the thumb's proportional size.</summary>
    public double ViewportSize
    {
        get => GetValue<double>(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    /// <summary>Raised on any scroll (thumb drag, page, or line), with the cause and the new value.</summary>
    public event EventHandler<ScrollEventArgs> Scroll;

    private static void OnOrientationChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
        => ((ScrollBar)d).ApplyOrientation();

    private void ApplyOrientation()
    {
        // Fix the cross axis, let the long axis stretch (clear it to Auto).
        if (Orientation == Orientation.Vertical)
        {
            Width = DefaultThickness;
            Height = double.NaN;
        }
        else
        {
            Height = DefaultThickness;
            Width = double.NaN;
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _track = GetTemplateChild("PART_Track") as Track;
        if (_track != null)
        {
            _track.Thumb.DragStarted += OnThumbDragStarted;
            _track.Thumb.DragDelta += OnThumbDragDelta;
            _track.Thumb.DragCompleted += OnThumbDragCompleted;
            _track.IncreaseRepeatButton.Click += OnPageIncrease;
            _track.DecreaseRepeatButton.Click += OnPageDecrease;
        }

        // Line buttons are optional - the default theme omits them, but a custom template may add them.
        _lineUpButton = GetTemplateChild("PART_LineUpButton") as RepeatButton;
        if (_lineUpButton != null) _lineUpButton.Click += OnLineDecrease;

        _lineDownButton = GetTemplateChild("PART_LineDownButton") as RepeatButton;
        if (_lineDownButton != null) _lineDownButton.Click += OnLineIncrease;
    }

    private void DetachParts()
    {
        if (_track != null)
        {
            _track.Thumb.DragStarted -= OnThumbDragStarted;
            _track.Thumb.DragDelta -= OnThumbDragDelta;
            _track.Thumb.DragCompleted -= OnThumbDragCompleted;
            _track.IncreaseRepeatButton.Click -= OnPageIncrease;
            _track.DecreaseRepeatButton.Click -= OnPageDecrease;
            _track = null;
        }
        if (_lineUpButton != null) { _lineUpButton.Click -= OnLineDecrease; _lineUpButton = null; }
        if (_lineDownButton != null) { _lineDownButton.Click -= OnLineIncrease; _lineDownButton = null; }
    }

    // Thumb drag: e.Change is the CUMULATIVE pointer movement since the press (measured in the Track's stable space),
    // so map it from the value captured at drag start - not by accumulating per-event deltas, which drifted.
    private void OnThumbDragStarted(object sender, DragStartedEventArgs e) => _dragStartValue = Value;

    private void OnThumbDragDelta(object sender, DragEventArgs e)
    {
        if (_track == null) return;
        var newValue = _dragStartValue + _track.ValueFromDistance(e.Change.X, e.Change.Y);
        SetValueAndNotify(newValue, ScrollEventType.ThumbTrack);
    }

    private void OnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        => Scroll?.Invoke(this, new ScrollEventArgs(ScrollEventType.EndScroll, Value));

    private void OnPageIncrease(object sender, RoutedEventArgs e)
        => SetValueAndNotify(Value + LargeChange, ScrollEventType.LargeIncrement);

    private void OnPageDecrease(object sender, RoutedEventArgs e)
        => SetValueAndNotify(Value - LargeChange, ScrollEventType.LargeDecrement);

    private void OnLineIncrease(object sender, RoutedEventArgs e)
        => SetValueAndNotify(Value + SmallChange, ScrollEventType.SmallIncrement);

    private void OnLineDecrease(object sender, RoutedEventArgs e)
        => SetValueAndNotify(Value - SmallChange, ScrollEventType.SmallDecrement);

    // Sets Value (RangeBase coerces it into range) and raises Scroll with the coerced value. SetCurrentValue, NOT a plain
    // Value= : a thumb drag / page / line is USER INPUT, and writing Value at Local priority would permanently mask a
    // {Binding} on Value (a bar bound TwoWay to a shared offset stopped following the source once the user touched it -
    // two bars on one value desynced after the first drag). SetCurrentValue writes into the binding slot, so the two-way
    // write-back still pushes to the source and the next source change refreshes this bar cleanly.
    private void SetValueAndNotify(double newValue, ScrollEventType type)
    {
        SetCurrentValue(ValueProperty, newValue);
        Scroll?.Invoke(this, new ScrollEventArgs(type, Value));
    }
}
