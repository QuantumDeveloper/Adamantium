using System;
using Adamantium.Graphics.Fonts;
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
    private Popup _valueToolTip;                      // value badge shown over the thumb while dragging (opt-in)
    private TextBlock _valueToolTipText;

    public static readonly AdamantiumProperty IsValueToolTipEnabledProperty = AdamantiumProperty.Register(
        nameof(IsValueToolTipEnabled), typeof(bool), typeof(Slider), new PropertyMetadata(false));

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(Slider),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty TickFrequencyProperty = AdamantiumProperty.Register(nameof(TickFrequency),
        typeof(double), typeof(Slider), new PropertyMetadata(0.0));

    public static readonly AdamantiumProperty IsSnapToTickEnabledProperty = AdamantiumProperty.Register(
        nameof(IsSnapToTickEnabled), typeof(bool), typeof(Slider), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsMoveToPointEnabledProperty = AdamantiumProperty.Register(
        nameof(IsMoveToPointEnabled), typeof(bool), typeof(Slider), new PropertyMetadata(false));

    static Slider()
    {
        Keyboard.KeyDownEvent.RegisterClassHandler<Slider>(new KeyEventHandler(KeyDownClassHandler));
        // Slider convention: a 0..100 range (vs RangeBase's 0..1). A metadata default, NOT a constructor set - a set
        // writes Local priority, which outranks and permanently masks a {Binding}/Style/Trigger on Maximum (the bug that
        // froze a data-bound slider). Re-use RangeBase's OnMaximumChanged so the re-coercion still fires. LargeChange (10)
        // and SmallChange (1) already match the base defaults, so no override is needed for them.
        MaximumProperty.OverrideMetadata(typeof(Slider), new PropertyMetadata(100.0, OnMaximumChanged));
        // A slider takes keyboard focus (arrows nudge the value) - opt in, since the base default is now false. Its Thumb
        // and track RepeatButtons stay non-focusable, so the focus lands on the Slider itself.
        FocusableProperty.OverrideMetadata(typeof(Slider), new PropertyMetadata(true));
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

    /// <summary>When true, clicking anywhere on the track jumps the thumb straight to that point (and stays there),
    /// instead of paging by <see cref="RangeBase.LargeChange"/>. Mirrors WPF's Slider.IsMoveToPointEnabled.</summary>
    public bool IsMoveToPointEnabled
    {
        get => GetValue<bool>(IsMoveToPointEnabledProperty);
        set => SetValue(IsMoveToPointEnabledProperty, value);
    }

    /// <summary>When true, a small badge with the current value rides over the thumb while dragging - above the thumb for
    /// a horizontal slider, to its right for a vertical one. It follows the thumb (the popup re-anchors every frame).</summary>
    public bool IsValueToolTipEnabled
    {
        get => GetValue<bool>(IsValueToolTipEnabledProperty);
        set => SetValue(IsValueToolTipEnabledProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _track = GetTemplateChild("PART_Track") as Track;
        _selectionRange = GetTemplateChild("PART_SelectionRange") as MeasurableUIComponent;
        if (_track != null)
        {
            _track.SizeChanged += OnTrackSizeChanged;
            // The parts now come from the template, so a malformed one may omit them - guard each.
            if (_track.Thumb != null)
            {
                _track.Thumb.DragStarted += OnThumbDragStarted;
                _track.Thumb.DragDelta += OnThumbDragDelta;
                _track.Thumb.DragCompleted += OnThumbDragCompleted;
            }
            if (_track.IncreaseRepeatButton != null) _track.IncreaseRepeatButton.Click += OnIncrease;
            if (_track.DecreaseRepeatButton != null) _track.DecreaseRepeatButton.Click += OnDecrease;
        }
        UpdateFill();
    }

    private void DetachParts()
    {
        if (_track == null) return;
        _track.SizeChanged -= OnTrackSizeChanged;
        if (_track.Thumb != null)
        {
            _track.Thumb.DragStarted -= OnThumbDragStarted;
            _track.Thumb.DragDelta -= OnThumbDragDelta;
            _track.Thumb.DragCompleted -= OnThumbDragCompleted;
        }
        if (_track.IncreaseRepeatButton != null) _track.IncreaseRepeatButton.Click -= OnIncrease;
        if (_track.DecreaseRepeatButton != null) _track.DecreaseRepeatButton.Click -= OnDecrease;
        _track = null;
    }

    // Thumb drag: e.Change is the CUMULATIVE pointer movement since the press (in the Track's stable space), so map it
    // from the value captured at drag start rather than accumulating per-event deltas (which drift).
    private void OnThumbDragStarted(object sender, DragStartedEventArgs e)
    {
        _dragStartValue = Value;
        ShowValueToolTip();
    }

    private void OnThumbDragDelta(object sender, DragEventArgs e)
    {
        if (_track == null) return;
        SetValueFromInput(SnapToTick(_dragStartValue + _track.ValueFromDistance(e.Change.X, e.Change.Y)));
        UpdateValueToolTipText();
    }

    // Reflect USER INPUT into Value without masking a two-way {Binding}. A plain `Value = x` (SetValue at Local
    // priority) outranks Binding and permanently freezes a data-bound slider - the source could never update it again,
    // so a second slider bound to the same value stopped tracking (thumb/fill desync). SetCurrentValue writes the value
    // in the binding's own slot instead, so the two-way write-back fires AND later source changes still apply.
    private void SetValueFromInput(double value) => SetCurrentValue(ValueProperty, value);

    private void OnThumbDragCompleted(object sender, DragCompletedEventArgs e) => HideValueToolTip();

    // Click on the track: page towards/away from the thumb by LargeChange - OR, when IsMoveToPointEnabled, jump straight
    // to the clicked point. Each page area (DecreaseRepeatButton/IncreaseRepeatButton) fires its own Click; either side
    // jumps to the same place because the value comes from the actual click position, not the side.
    private void OnIncrease(object sender, RoutedEventArgs e)
    {
        if (IsMoveToPointEnabled) MoveToMousePoint();
        else SetValueFromInput(SnapToTick(Value + LargeChange));
    }

    private void OnDecrease(object sender, RoutedEventArgs e)
    {
        if (IsMoveToPointEnabled) MoveToMousePoint();
        else SetValueFromInput(SnapToTick(Value - LargeChange));
    }

    // The page button that fired the Click captured the press, so the mouse is still at the click point: read it relative
    // to the track and map it to a value (snapping still applies). Holding repeats this, so a held + dragged press follows.
    private void MoveToMousePoint()
    {
        if (_track == null) return;
        SetValueFromInput(SnapToTick(_track.ValueFromPoint(MouseDevice.CurrentDevice.GetPosition(_track))));
    }

    // The accent fill (PART_SelectionRange) is driven exactly like ProgressBar's PART_Indicator: project the value
    // fraction onto its WIDTH on every Value change (and once more after layout, when the track's pixel length is known).
    // Setting Width carries AffectsRender, so it repaints live - a part sized only by the Track's arrange would not.
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        // Drive the thumb IN LOCKSTEP with the fill. The fill is set synchronously below, but the thumb is positioned by
        // the Track's arrange off Track.Value - a (batched, next-frame) TemplateBinding to our Value - so without this the
        // thumb lags the fill by a frame on every change (the visible thumb/fill desync while dragging). Push the value to
        // the Track now at Binding priority (the same slot the TemplateBinding writes, so it neither masks the binding nor
        // is masked by it), then re-arrange the Track this frame so the thumb moves together with the fill.
        if (_track != null)
        {
            _track.SetValue(Track.ValueProperty, newValue, ValuePriority.Binding);
            ForceArrangeTrack();
        }
        UpdateFill();
        ForceArrangeFill();
    }

    // A Minimum/Maximum change rescales the fill even when Value is unchanged (a second slider that shares Value while
    // this one drives Maximum): the fraction (Value-Min)/(Max-Min) moved, so recompute the accent fill. The thumb already
    // re-positions via the Track's Minimum/Maximum TemplateBindings (AffectsArrange); the fill has no such path.
    protected override void OnRangeBoundsChanged()
    {
        UpdateFill();
        ForceArrangeFill();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Honest cross-axis: a horizontal slider is only as tall as its content (rail + thumb), NOT the whole slot - else
        // its ClipRectangle swallows clicks/hovers across the slot and blocks other controls' input. Stretch stays on the
        // MAIN axis. (Mirror of ProgressBar.)
        finalSize = Orientation == Orientation.Horizontal
            ? new Size(finalSize.Width, Math.Min(finalSize.Height, DesiredSize.Height))
            : new Size(Math.Min(finalSize.Width, DesiredSize.Width), finalSize.Height);

        var size = base.ArrangeOverride(finalSize);
        // The track fills the slider, so the arranged main-axis length is the fill's 100%.
        UpdateFill(Orientation == Orientation.Horizontal ? size.Width : size.Height);
        ForceArrangeFill();
        return size;
    }

    private void OnTrackSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateFill();
        ForceArrangeFill();
    }

    private void UpdateFill(double? trackLength = null)
    {
        if (_selectionRange == null || _track == null) return;

        var range = Maximum - Minimum;
        var fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0.0, 1.0) : 0.0;

        var full = Orientation == Orientation.Horizontal
            ? trackLength ?? _track.ActualWidth
            : trackLength ?? _track.ActualHeight;
        if (full <= 0) return;

        if (Orientation == Orientation.Horizontal)
            SetIfChanged(WidthProperty, fraction * full, _selectionRange.Width);
        else
            SetIfChanged(HeightProperty, fraction * full, _selectionRange.Height);
    }

    // The fill's length comes from the arrange-time track size, but a part is arranged by its parent at its DESIRED size,
    // which was measured before that length was known - so base.ArrangeOverride parks the fill at its stale desired size,
    // and the manager's deferred re-layout doesn't reliably re-arrange a small child whose growth doesn't change its
    // parent's size (the fill stayed empty until the first drag). So re-measure the fill (force, so the new Width/Height
    // takes) and re-arrange it into the cell it already occupies - synchronous and guaranteed, this frame.
    private void ForceArrangeFill()
    {
        if (_selectionRange is not IMeasurableComponent m || m.PreviousArrangeSlot is not { } slot) return;
        _selectionRange.Measure(new Size(slot.Width, slot.Height), force: true);
        _selectionRange.Arrange(slot);
    }

    // Re-arrange the Track into the slot it already occupies, synchronously, so the thumb repositions THIS frame (mirrors
    // ForceArrangeFill). Without it the thumb waits for the next layout pass to pick up the batched Track.Value binding.
    private void ForceArrangeTrack()
    {
        if (_track is IMeasurableComponent m && m.PreviousArrangeSlot is { } slot)
            _track.Arrange(slot, force: true);
    }

    // Only re-set on a real change so layout settles instead of looping (NaN = never set yet -> always set the first time).
    private void SetIfChanged(AdamantiumProperty property, double target, double current)
    {
        if (double.IsNaN(current) || Math.Abs(current - target) > 0.5)
            _selectionRange.SetValue(property, target);
    }

    // --- Value tooltip (opt-in): a value badge over the thumb on the window-wide popup layer, following the thumb -----

    private void ShowValueToolTip()
    {
        if (!IsValueToolTipEnabled) return;
        EnsureValueToolTip();
        UpdateValueToolTipText();
        _valueToolTip.IsOpen = true;
    }

    private void HideValueToolTip()
    {
        if (_valueToolTip != null) _valueToolTip.IsOpen = false;
    }

    private void UpdateValueToolTipText()
    {
        if (_valueToolTipText != null) _valueToolTipText.Text = Value.ToString("0.##");
    }

    // Build the badge lazily (only for sliders that use it) and add it to this slider's tree so it finds the window's
    // popup layer; re-point it at the current thumb + side (horizontal -> above the thumb, vertical -> to its right).
    private void EnsureValueToolTip()
    {
        var horizontal = Orientation == Orientation.Horizontal;
        if (_valueToolTip == null)
        {
            _valueToolTipText = new TextBlock
            {
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                // Centre the BLOCK in the badge via layout alignment (both axes). VerticalTextAlignment DEFAULTS to
                // Bottom, which sits the glyphs' ink on the block's baseline - so the value looked low/offset in the
                // badge even though the block itself was centred. Center centres the INK inside the block's line box,
                // so the digit lands on the badge's true vertical centre.
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalTextAlignment = VerticalTextAlignment.Center
            };
            var badge = new Border
            {
                Background = new SolidColorBrush(Colors.DimGray),
                CornerRadius = new CornerRadius(4),
                // Explicit 4-arg (L,T,R,B): the 2-arg Thickness(8,4) is (leftTop, rightBottom) = Top 8 / Bottom 4, an
                // asymmetric vertical pad that pushed the value DOWN in the badge. Intent was horizontal 8, vertical 4.
                Padding = new Thickness(8, 4, 8, 4),
                // A stable baseline width so 1-2 digit values don't resize + re-center the badge on every step (the badge
                // still grows to fit longer values). Pair with IsSnapToTickEnabled to drop the per-frame decimal jitter.
                MinWidth = 32,
                Child = _valueToolTipText
            };
            _valueToolTip = new Popup { Child = badge };
            LogicalChildrenCollection.Add(_valueToolTip);
            VisualChildrenCollection.Add(_valueToolTip);
        }
        _valueToolTip.PlacementTarget = _track?.Thumb;
        _valueToolTip.Placement = horizontal ? PlacementMode.Top : PlacementMode.Right;
        _valueToolTip.VerticalOffset = horizontal ? -6 : 0;
        _valueToolTip.HorizontalOffset = horizontal ? 0 : 6;
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
            case Key.RightArrow or Key.UpArrow: SetValueFromInput(SnapToTick(Value + SmallChange)); e.Handled = true; break;
            case Key.LeftArrow or Key.DownArrow: SetValueFromInput(SnapToTick(Value - SmallChange)); e.Handled = true; break;
            case Key.PageUp: SetValueFromInput(SnapToTick(Value + LargeChange)); e.Handled = true; break;
            case Key.PageDown: SetValueFromInput(SnapToTick(Value - LargeChange)); e.Handled = true; break;
            case Key.Home: SetValueFromInput(Minimum); e.Handled = true; break;
            case Key.End: SetValueFromInput(Maximum); e.Handled = true; break;
        }
    }
}
