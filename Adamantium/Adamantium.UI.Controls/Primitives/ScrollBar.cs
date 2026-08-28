using System;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
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
    // The cross-axis thickness a bar has when no theme says otherwise. A DEFAULT, not the answer: see BarThickness.
    private const double DefaultThickness = 12.0;

    /// <summary>How thick the bar is across its short axis - its Width when vertical, its Height when horizontal.
    /// <para>A PROPERTY rather than a constant because it is a THEME's number: a dense editor skin wants a thinner bar
    /// than a touch-friendly one. The control still writes the cross axis itself (that is what fixes one axis and lets
    /// the other stretch), and it writes it at Local priority - so while this was a const, a style setter for Width was
    /// simply overwritten and no theme could change it at all. Setting THIS is what a theme does now.</para></summary>
    public static readonly AdamantiumProperty BarThicknessProperty = AdamantiumProperty.Register(nameof(BarThickness),
        typeof(double), typeof(ScrollBar),
        new PropertyMetadata(DefaultThickness, PropertyMetadataOptions.AffectsMeasure, OnBarThicknessChanged));

    public double BarThickness
    {
        get => GetValue<double>(BarThicknessProperty);
        set => SetValue(BarThicknessProperty, value);
    }

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
        // which writes Local priority and masks the {Binding}/direct Maximum a ScrollViewer applies).
        MaximumProperty.OverrideMetadata(typeof(ScrollBar), new PropertyMetadata(0.0));
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

    // Either the axis or the number changing re-applies it, so the two can arrive in any order - a theme's setter lands
    // when the style attaches, which is not necessarily before the orientation is set.
    private static void OnBarThicknessChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
        => ((ScrollBar)d).ApplyOrientation();

    /// <summary>Which axis currently carries the thickness WE stamped. The only size this control may release: the long
    /// axis belongs to whoever placed the bar.</summary>
    private Orientation? _stampedAxis;

    private void ApplyOrientation()
    {
        // Fix the CROSS axis and leave the long one alone. It used to clear the long axis to NaN as well, which was
        // harmless only while this ran once from the constructor - before any markup. As soon as a theme's BarThickness
        // setter could re-run it, it landed AFTER the markup and wiped an author's Width="320": the bar took its length
        // from whatever the parent panel happened to be, and a sibling label that changed width during a drag resized
        // the thumb on every frame.
        if (_stampedAxis is { } stamped && stamped != Orientation)
            ClearValue(stamped == Orientation.Vertical ? WidthProperty : HeightProperty);

        if (Orientation == Orientation.Vertical) Width = BarThickness;
        else Height = BarThickness;

        _stampedAxis = Orientation;
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

    /// <summary>Let the template's parts go when the template does. The same DetachParts the swap path uses - a
    /// template that is REPLACED re-runs OnApplyTemplate and is covered there, but one that is simply dropped never
    /// gets that call, and the wiring would outlive the parts it points at.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
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

    // Paging stops AT THE CURSOR, as everywhere else: the repeat runs until the thumb reaches the pointer and no
    // further. See Track.PageLimitFromPoint for why the page button cannot notice that on its own.
    private void OnPageIncrease(object sender, RoutedEventArgs e)
        => Page(Value + LargeChange, increasing: true, ScrollEventType.LargeIncrement);

    private void OnPageDecrease(object sender, RoutedEventArgs e)
        => Page(Value - LargeChange, increasing: false, ScrollEventType.LargeDecrement);

    private void Page(double stepped, bool increasing, ScrollEventType type)
    {
        if (_track != null)
        {
            var limit = _track.PageLimitFromPoint(MouseDevice.CurrentDevice.GetPosition(_track), increasing);
            stepped = increasing ? Math.Min(stepped, limit) : Math.Max(stepped, limit);
        }

        // Already there: keep quiet rather than raise a Scroll every repeat tick for a value that does not move.
        if (stepped == Value) return;
        SetValueAndNotify(stepped, type);
    }

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
