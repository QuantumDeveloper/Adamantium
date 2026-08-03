using System;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// Picks a SPAN out of [<see cref="Minimum"/>, <see cref="Maximum"/>] with two thumbs. Where a pair of Sliders leaves it
/// to the caller to keep "from" below "to" - and lets the user drag them past each other - this control cannot express
/// an inverted range at all: the bounds coerce against each other, keeping at least <see cref="MinimumRange"/> between
/// them. Interaction lives here; the geometry is <see cref="RangeTrack"/>, exactly as Slider leaves it to Track.
/// </summary>
public class RangeSlider : RangeLimitsBase
{
    private RangeTrack _track;
    private double _dragStartLower;
    private double _dragStartUpper;
    private Popup _lowerBadge, _upperBadge;            // value badges over the thumbs while dragging (opt-in)
    private TextBlock _lowerBadgeText, _upperBadgeText;

    public static readonly AdamantiumProperty LowerValueProperty = AdamantiumProperty.Register(nameof(LowerValue),
        typeof(double), typeof(RangeSlider),
        new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceLowerValue));

    public static readonly AdamantiumProperty UpperValueProperty = AdamantiumProperty.Register(nameof(UpperValue),
        typeof(double), typeof(RangeSlider),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceUpperValue));

    /// <summary>How close the two bounds may come, in VALUE units. Zero lets them meet.</summary>
    public static readonly AdamantiumProperty MinimumRangeProperty = AdamantiumProperty.Register(nameof(MinimumRange),
        typeof(double), typeof(RangeSlider), new PropertyMetadata(0.0, OnMinimumRangeChanged));

    /// <summary>How wide the band between the thumbs stays ON SCREEN, in pixels, however narrow the span itself gets.
    /// Purely visual: it never touches the values, so the two bounds may still meet on one number (55..55 is a legitimate
    /// selection) - the thumbs simply keep a grabbable gap between them at that point.</summary>
    public static readonly AdamantiumProperty MinimumRangeWidthProperty = AdamantiumProperty.Register(
        nameof(MinimumRangeWidth), typeof(double), typeof(RangeSlider), new PropertyMetadata(12.0));

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(RangeSlider),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Thickness of the band between the thumbs, ACROSS the track. Its own knob because the band is a handle in
    /// its own right - making it easier to hit is a different decision from how thick the rail is drawn.</summary>
    public static readonly AdamantiumProperty BandThicknessProperty = AdamantiumProperty.Register(nameof(BandThickness),
        typeof(double), typeof(RangeSlider), new PropertyMetadata(6.0, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Show each bound's value in a badge over its thumb while it is being dragged.</summary>
    public static readonly AdamantiumProperty IsValueToolTipEnabledProperty = AdamantiumProperty.Register(
        nameof(IsValueToolTipEnabled), typeof(bool), typeof(RangeSlider), new PropertyMetadata(false));

    public static readonly AdamantiumProperty TickFrequencyProperty = AdamantiumProperty.Register(nameof(TickFrequency),
        typeof(double), typeof(RangeSlider), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty IsSnapToTickEnabledProperty = AdamantiumProperty.Register(
        nameof(IsSnapToTickEnabled), typeof(bool), typeof(RangeSlider), new PropertyMetadata(false));

    /// <summary>Raised after either bound changed - one event for the span, since moving a thumb usually means asking
    /// "what is selected now", not "which end moved".</summary>
    public event EventHandler RangeChanged;

    /// <summary>Start of the selected span. Never above <see cref="UpperValue"/> minus <see cref="MinimumRange"/>.</summary>
    public double LowerValue
    {
        get => GetValue<double>(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    /// <summary>End of the selected span. Never below <see cref="LowerValue"/> plus <see cref="MinimumRange"/>.</summary>
    public double UpperValue
    {
        get => GetValue<double>(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public double MinimumRange
    {
        get => GetValue<double>(MinimumRangeProperty);
        set => SetValue(MinimumRangeProperty, value);
    }

    public double MinimumRangeWidth
    {
        get => GetValue<double>(MinimumRangeWidthProperty);
        set => SetValue(MinimumRangeWidthProperty, value);
    }


    public double BandThickness
    {
        get => GetValue<double>(BandThicknessProperty);
        set => SetValue(BandThicknessProperty, value);
    }

    public bool IsValueToolTipEnabled
    {
        get => GetValue<bool>(IsValueToolTipEnabledProperty);
        set => SetValue(IsValueToolTipEnabledProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Step the values snap to when <see cref="IsSnapToTickEnabled"/> is on (0 = no snapping).</summary>
    public double TickFrequency
    {
        get => GetValue<double>(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public bool IsSnapToTickEnabled
    {
        get => GetValue<bool>(IsSnapToTickEnabledProperty);
        set => SetValue(IsSnapToTickEnabledProperty, value);
    }

    // Each bound is clamped against the other. The request itself is kept by the property system, so a bound that had to
    // be clamped while the other was still elsewhere is restored by re-coercion once there is room - see CoerceValue.
    private static object CoerceLowerValue(AdamantiumComponent a, object value)
    {
        if (a is not RangeSlider slider || value is not double lower) return value;

        var ceiling = slider.UpperValue - slider.MinimumRange;
        return Math.Clamp(lower, slider.Minimum, Math.Max(slider.Minimum, ceiling));
    }

    private static object CoerceUpperValue(AdamantiumComponent a, object value)
    {
        if (a is not RangeSlider slider || value is not double upper) return value;

        var floor = slider.LowerValue + slider.MinimumRange;
        return Math.Clamp(upper, Math.Min(slider.Maximum, floor), slider.Maximum);
    }

    // Widening the gap the bounds must keep can invalidate both of them, exactly like moving Minimum/Maximum does.
    private static void OnMinimumRangeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        (a as RangeSlider)?.ReCoerceSelection();
    }

    // Minimum/Maximum moved: both bounds may now be outside, so re-run both coercions (the base calls this).
    protected override void ReCoerceSelection()
    {
        CoerceValue(LowerValueProperty);
        CoerceValue(UpperValueProperty);
    }

    private static void OnValueChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RangeSlider slider) return;

        // The other end may now be illegal - the pair is only ever valid together. Re-coercion settles because a bound
        // that does not move announces nothing, so the exchange stops after one round.
        slider.CoerceValue(e.Property == LowerValueProperty ? UpperValueProperty : LowerValueProperty);
        slider.RangeChanged?.Invoke(slider, EventArgs.Empty);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _track = GetTemplateChild("PART_Track") as RangeTrack;
        if (_track != null)
        {
            if (_track.LowerThumb != null)
            {
                _track.LowerThumb.DragStarted += OnThumbDragStarted;
                _track.LowerThumb.DragDelta += OnLowerDragDelta;
                _track.LowerThumb.DragCompleted += OnLowerDragCompleted;
            }
            if (_track.UpperThumb != null)
            {
                _track.UpperThumb.DragStarted += OnThumbDragStarted;
                _track.UpperThumb.DragDelta += OnUpperDragDelta;
                _track.UpperThumb.DragCompleted += OnUpperDragCompleted;
            }
            if (_track.CenterThumb != null)
            {
                _track.CenterThumb.DragStarted += OnThumbDragStarted;
                _track.CenterThumb.DragDelta += OnCenterDragDelta;
                _track.CenterThumb.DragCompleted += OnCenterDragCompleted;
            }
        }
    }

    private void DetachParts()
    {
        if (_track == null) return;

        if (_track.LowerThumb != null)
        {
            _track.LowerThumb.DragStarted -= OnThumbDragStarted;
            _track.LowerThumb.DragDelta -= OnLowerDragDelta;
            _track.LowerThumb.DragCompleted -= OnLowerDragCompleted;
        }
        if (_track.UpperThumb != null)
        {
            _track.UpperThumb.DragStarted -= OnThumbDragStarted;
            _track.UpperThumb.DragDelta -= OnUpperDragDelta;
            _track.UpperThumb.DragCompleted -= OnUpperDragCompleted;
        }
        if (_track.CenterThumb != null)
        {
            _track.CenterThumb.DragStarted -= OnThumbDragStarted;
            _track.CenterThumb.DragDelta -= OnCenterDragDelta;
            _track.CenterThumb.DragCompleted -= OnCenterDragCompleted;
        }
        _track = null;
    }

    // e.Change is the CUMULATIVE movement since the press, so map it from the values captured at drag start rather than
    // accumulating per-event deltas, which drift (same reasoning as Slider).
    private void OnThumbDragStarted(object sender, DragStartedEventArgs e)
    {
        _dragStartLower = LowerValue;
        _dragStartUpper = UpperValue;
        ShowValueBadges(sender as Thumb);
    }

    private void OnLowerDragDelta(object sender, DragEventArgs e)
    {
        DragLowerTo(e.Change.X, e.Change.Y);
        UpdateValueBadges();
    }

    private void OnUpperDragDelta(object sender, DragEventArgs e)
    {
        DragUpperTo(e.Change.X, e.Change.Y);
        UpdateValueBadges();
    }

    // The completed delta is the true release position: fast drags coalesce DragDelta events, so the last one processed
    // can lag the release and leave the thumb short of where it was let go.
    private void OnLowerDragCompleted(object sender, DragCompletedEventArgs e)
    {
        DragLowerTo(e.Change.X, e.Change.Y);
        HideValueBadges();
    }

    private void OnUpperDragCompleted(object sender, DragCompletedEventArgs e)
    {
        DragUpperTo(e.Change.X, e.Change.Y);
        HideValueBadges();
    }

    private void DragLowerTo(double horizontal, double vertical)
    {
        if (_track == null) return;
        SetLowerFromInput(SnapToTick(_dragStartLower + _track.ValueFromDistance(horizontal, vertical)));
    }

    private void DragUpperTo(double horizontal, double vertical)
    {
        if (_track == null) return;
        SetUpperFromInput(SnapToTick(_dragStartUpper + _track.ValueFromDistance(horizontal, vertical)));
    }

    private void OnCenterDragDelta(object sender, DragEventArgs e)
    {
        DragSpanTo(e.Change.X, e.Change.Y);
        UpdateValueBadges();
    }

    private void OnCenterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        DragSpanTo(e.Change.X, e.Change.Y);
        HideValueBadges();
    }

    // The whole span moves: both bounds shift by the same amount, and the SHIFT is clamped rather than each bound, so
    // running into an end stops the span instead of squashing it against that end.
    private void DragSpanTo(double horizontal, double vertical)
    {
        if (_track == null) return;

        var width = _dragStartUpper - _dragStartLower;
        var shift = _track.ValueFromDistance(horizontal, vertical);
        var lower = SnapToTick(Math.Clamp(_dragStartLower + shift, Minimum, Maximum - width));

        // Order matters: the pair coerce against each other, so move the end that is travelling AWAY from the other one
        // first - the other would otherwise be pushed by the coercion before it gets its own value.
        if (shift >= 0)
        {
            SetUpperFromInput(lower + width);
            SetLowerFromInput(lower);
        }
        else
        {
            SetLowerFromInput(lower);
            SetUpperFromInput(lower + width);
        }
    }

    // A click on the EMPTY trough moves the nearer bound to it - the reading of "click there" that never crosses the
    // other thumb, so a stray click cannot invert the selection.
    // A press that lands ON a thumb - either end, or the band between them - moves nothing: that press is the start of a
    // DRAG, and the thumb must follow the pointer from wherever it was grabbed. Jumping it to the pointer first is what
    // makes a handle twitch under the cursor before every drag.
    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (_track == null || e.Handled) return;

        var point = e.GetPosition(_track);
        var vertical = Orientation == Orientation.Vertical;
        var pos = vertical ? point.Y : point.X;

        // Occupancy is judged ALONG the track only, and a press that lands on a handle's span GRABS that handle - from
        // wherever it landed across the rail. The band is a few pixels thick on a rail several times taller, so aiming at
        // it lands beside it about as often as on it; measured, the misses were one to three pixels below a six-pixel
        // band. Judging along the track and then merely returning is what made those presses do nothing at all, and
        // moving the nearest bound to them instead - what this did before - made a handle jump out from under the
        // pointer. Forwarding is a no-op when the thumb took the press itself (BeginDrag is idempotent while it holds
        // capture), which it does whenever the aim was true.
        var handle = CoveringHandle(pos, vertical);
        if (handle != null)
        {
            handle.BeginDrag(e);
            return;
        }

        // WHICH bound moves is decided in pixels - by the thumb the press landed nearer to - and only then is the point
        // turned into a value FOR THAT BOUND. Choosing by value would compare against a scale the two ends do not share,
        // and the two mappings differ anyway (the upper thumb has the lower one and the band ahead of it).
        var lowerCentre = Centre(_track.LowerThumb, vertical);
        var upperCentre = Centre(_track.UpperThumb, vertical);

        if (Math.Abs(pos - lowerCentre) <= Math.Abs(pos - upperCentre))
        {
            SetLowerFromInput(SnapToTick(_track.LowerValueFromPoint(point)));
        }
        else
        {
            SetUpperFromInput(SnapToTick(_track.UpperValueFromPoint(point)));
        }
    }

    private static double Centre(Thumb thumb, bool vertical)
    {
        if (thumb == null) return 0;
        var bounds = thumb.Bounds;
        return vertical ? bounds.Y + bounds.Height / 2 : bounds.X + bounds.Width / 2;
    }

    // Which handle owns this position along the track, if any. The three never overlap along it, so at most one answers.
    private Thumb CoveringHandle(double pos, bool vertical)
    {
        if (CoversAlong(_track.CenterThumb, pos, vertical)) return _track.CenterThumb;
        if (CoversAlong(_track.LowerThumb, pos, vertical)) return _track.LowerThumb;
        if (CoversAlong(_track.UpperThumb, pos, vertical)) return _track.UpperThumb;
        return null;
    }

    // Does this part span the position ALONG the track? (Across it, anywhere on the rail counts as the same place.)
    private static bool CoversAlong(Thumb thumb, double pos, bool vertical)
    {
        if (thumb == null) return false;

        var bounds = thumb.Bounds;
        var start = vertical ? bounds.Y : bounds.X;
        var length = vertical ? bounds.Height : bounds.Width;
        return pos >= start && pos <= start + length;
    }

    // Two badges, one per bound - dragging the band moves both values, and a single badge would have to pick one to lie
    // about. Built lazily (only for sliders that ask for them) and kept as LOGICAL children only: a popup is a portal
    // rendered on the window's popup layer, and putting it in the visual tree would invalidate this control's measure on
    // every drag (see the same note on Slider).
    private void ShowValueBadges(Thumb grabbed)
    {
        if (!IsValueToolTipEnabled || _track == null) return;

        EnsureBadge(ref _lowerBadge, ref _lowerBadgeText, _track.LowerThumb);
        EnsureBadge(ref _upperBadge, ref _upperBadgeText, _track.UpperThumb);
        UpdateValueBadges();

        // Grabbing an end shows that end's value; grabbing the band moves both, so both are worth seeing.
        var band = ReferenceEquals(grabbed, _track.CenterThumb);
        if (_lowerBadge != null) _lowerBadge.IsOpen = band || ReferenceEquals(grabbed, _track.LowerThumb);
        if (_upperBadge != null) _upperBadge.IsOpen = band || ReferenceEquals(grabbed, _track.UpperThumb);
    }

    private void UpdateValueBadges()
    {
        if (_lowerBadgeText != null) _lowerBadgeText.Text = LowerValue.ToString("0.##");
        if (_upperBadgeText != null) _upperBadgeText.Text = UpperValue.ToString("0.##");
    }

    private void HideValueBadges()
    {
        if (_lowerBadge != null) _lowerBadge.IsOpen = false;
        if (_upperBadge != null) _upperBadge.IsOpen = false;
    }

    private void EnsureBadge(ref Popup popup, ref TextBlock text, Thumb anchor)
    {
        if (anchor == null) return;

        var horizontal = Orientation == Orientation.Horizontal;
        if (popup == null)
        {
            text = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalTextAlignment = VerticalTextAlignment.Center
            };
            var badge = new Border
            {
                Background = new SolidColorBrush(Colors.DimGray),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 32,
                Child = text
            };
            popup = new Popup { Child = badge };
            LogicalChildrenCollection.Add(popup);
        }

        popup.PlacementTarget = anchor;
        popup.Placement = horizontal ? PlacementMode.Top : PlacementMode.Right;
        popup.VerticalOffset = horizontal ? -6 : 0;
        popup.HorizontalOffset = horizontal ? 0 : 6;
    }

    // SetCurrentValue, not a plain assignment: a Local write outranks a {Binding} and would freeze a data-bound control
    // for good - the source could never move it again (see the same note on Slider).
    private void SetLowerFromInput(double value) => SetCurrentValue(LowerValueProperty, value);

    private void SetUpperFromInput(double value) => SetCurrentValue(UpperValueProperty, value);

    private double SnapToTick(double value)
    {
        if (!IsSnapToTickEnabled || TickFrequency <= 0) return value;

        var steps = Math.Round((value - Minimum) / TickFrequency, MidpointRounding.AwayFromZero);
        return Math.Clamp(Minimum + steps * TickFrequency, Minimum, Maximum);
    }

}
