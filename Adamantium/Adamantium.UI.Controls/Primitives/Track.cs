using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// The interactive area of a <see cref="ScrollBar"/> or a Slider: a draggable <see cref="Thumb"/> flanked by two
/// <see cref="RepeatButton"/>s that page towards/away from the thumb. It sizes and positions those three parts from
/// <see cref="Minimum"/>/<see cref="Maximum"/>/<see cref="Value"/>/<see cref="ViewportSize"/>. The parts themselves are
/// supplied by the CONSUMING template (so each theme fully owns their look - a scrollbar's grey bar + invisible page
/// areas, a slider's accent fill + accent thumb), never created here. Mirrors WPF's Track.
/// </summary>
public class Track : Panel
{
    // The thumb never shrinks below this along the track, so it stays grabbable even with a huge scroll range.
    private const double MinThumbLength = 12.0;

    private double _density;     // value units per pixel of thumb travel (for ValueFromDistance)
    private double _remaining;   // travel length (trackLength - thumbAlong); for ValueFromPoint
    private double _thumbAlong;  // thumb size along the track; for ValueFromPoint (centre the thumb on the click)

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(Track),
        new PropertyMetadata(Orientation.Vertical, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty MinimumProperty = AdamantiumProperty.Register(nameof(Minimum),
        typeof(double), typeof(Track), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty MaximumProperty = AdamantiumProperty.Register(nameof(Maximum),
        typeof(double), typeof(Track), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(double), typeof(Track), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty ViewportSizeProperty = AdamantiumProperty.Register(nameof(ViewportSize),
        typeof(double), typeof(Track), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty IsDirectionReversedProperty = AdamantiumProperty.Register(
        nameof(IsDirectionReversed), typeof(bool), typeof(Track),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsArrange));

    // The three parts come from the template (<Track.Thumb>, <Track.DecreaseRepeatButton>, <Track.IncreaseRepeatButton>).
    // Assigning one swaps it into Children so it lives in the visual tree and gets measured/arranged here.
    public static readonly AdamantiumProperty ThumbProperty = AdamantiumProperty.Register(nameof(Thumb),
        typeof(Thumb), typeof(Track), new PropertyMetadata(null, OnPartChanged));

    public static readonly AdamantiumProperty IncreaseRepeatButtonProperty = AdamantiumProperty.Register(
        nameof(IncreaseRepeatButton), typeof(RepeatButton), typeof(Track), new PropertyMetadata(null, OnPartChanged));

    public static readonly AdamantiumProperty DecreaseRepeatButtonProperty = AdamantiumProperty.Register(
        nameof(DecreaseRepeatButton), typeof(RepeatButton), typeof(Track), new PropertyMetadata(null, OnPartChanged));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Minimum
    {
        get => GetValue<double>(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue<double>(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue<double>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The visible portion of the scrolled content, in the same units as the range. 0 = slider-style fixed thumb.</summary>
    public double ViewportSize
    {
        get => GetValue<double>(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    /// <summary>Flips the value→position mapping along the track. Default false = Minimum at the start (top/left) - the
    /// scrollbar convention. A vertical slider sets this true so Minimum sits at the BOTTOM and the value grows upward.</summary>
    public bool IsDirectionReversed
    {
        get => GetValue<bool>(IsDirectionReversedProperty);
        set => SetValue(IsDirectionReversedProperty, value);
    }

    public Thumb Thumb
    {
        get => GetValue<Thumb>(ThumbProperty);
        set => SetValue(ThumbProperty, value);
    }

    /// <summary>Pages towards the maximum (below/right of the thumb).</summary>
    public RepeatButton IncreaseRepeatButton
    {
        get => GetValue<RepeatButton>(IncreaseRepeatButtonProperty);
        set => SetValue(IncreaseRepeatButtonProperty, value);
    }

    /// <summary>Pages towards the minimum (above/left of the thumb).</summary>
    public RepeatButton DecreaseRepeatButton
    {
        get => GetValue<RepeatButton>(DecreaseRepeatButtonProperty);
        set => SetValue(DecreaseRepeatButtonProperty, value);
    }

    private static void OnPartChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not Track track) return;
        if (e.OldValue is MeasurableUIComponent oldPart) track.Children.Remove(oldPart);
        if (e.NewValue is MeasurableUIComponent newPart) track.Children.Add(newPart);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(availableSize);

        // The track stretches to fill the trough; the thumb's cross-size is the only intrinsic demand.
        var thumb = Thumb?.DesiredSize ?? default;
        return Orientation == Orientation.Vertical
            ? new Size(thumb.Width, 0)
            : new Size(0, thumb.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var trackLength = vertical ? finalSize.Height : finalSize.Width;
        var thickness = vertical ? finalSize.Width : finalSize.Height;

        var range = Maximum - Minimum;
        var viewport = Math.Max(0, ViewportSize);
        var thumbDesired = Thumb?.DesiredSize ?? new Size(MinThumbLength, MinThumbLength);

        // Thumb size ALONG the track + ACROSS it. Scrollbar (viewport > 0): a viewport-proportional bar that fills the
        // cross thickness. Slider (viewport == 0): a fixed handle at the theme's own Thumb size, centred across the track
        // (so it can be a circle on a thin rail). Nothing to scroll: the thumb fills the whole track.
        double thumbAlong;
        double thumbCross;
        if (range <= 0)
        {
            thumbAlong = trackLength;
            thumbCross = thickness;
        }
        else if (viewport > 0)
        {
            thumbAlong = trackLength * viewport / (range + viewport);
            thumbCross = thickness;
        }
        else
        {
            thumbAlong = vertical ? thumbDesired.Height : thumbDesired.Width;
            thumbCross = Math.Min(vertical ? thumbDesired.Width : thumbDesired.Height, thickness);
        }
        thumbAlong = Math.Clamp(thumbAlong, Math.Min(MinThumbLength, trackLength), trackLength);

        var remaining = trackLength - thumbAlong;
        var offset = range > 0 ? (Value - Minimum) / range * remaining : 0;
        offset = Math.Clamp(offset, 0, remaining);

        // Thumb travel in value-units-per-pixel, used by the thumb-drag mapping (ValueFromDistance).
        _density = remaining > 0 && range > 0 ? range / remaining : 0;
        _remaining = remaining;
        _thumbAlong = thumbAlong;

        var crossOffset = Math.Max(0, (thickness - thumbCross) / 2);

        // IsDirectionReversed (a vertical slider): put Minimum at the BOTTOM, so a higher value sits HIGHER. Flip the
        // thumb's distance-along-the-track AND which page button is decrease/increase (toward min vs max swap sides).
        var reversed = vertical && IsDirectionReversed;
        var along = reversed ? remaining - offset : offset;
        var trailingStart = along + thumbAlong;
        var trailingLength = Math.Max(0, trackLength - trailingStart);
        var leadingButton = reversed ? IncreaseRepeatButton : DecreaseRepeatButton;
        var trailingButton = reversed ? DecreaseRepeatButton : IncreaseRepeatButton;

        if (vertical)
        {
            leadingButton?.Arrange(new Rect(0, 0, thickness, along));
            Thumb?.Arrange(new Rect(crossOffset, along, thumbCross, thumbAlong));
            trailingButton?.Arrange(new Rect(0, trailingStart, thickness, trailingLength));
        }
        else
        {
            leadingButton?.Arrange(new Rect(0, 0, along, thickness));
            Thumb?.Arrange(new Rect(along, crossOffset, thumbAlong, thumbCross));
            trailingButton?.Arrange(new Rect(trailingStart, 0, trailingLength, thickness));
        }

        return finalSize;
    }

    /// <summary>Where the CENTRE of the thumb sits for a fraction of the range, in pixels from the start of the track -
    /// the same mapping <see cref="ArrangeOverride"/> uses. Anything that has to line up with the thumb (a Slider's accent
    /// fill) must read it from here rather than project the fraction itself: the thumb travels the trough MINUS its own
    /// length, so a second projection onto the FULL length drifts by half a thumb at either end - short of the thumb at
    /// the minimum, past it at the maximum, agreeing only in the middle. The FRACTION is the caller's, deliberately: the
    /// value-to-fraction step belongs to whoever owns the range, and taking a value here would answer from this track's
    /// own copy of Minimum/Maximum instead. NaN before the first arrange, when there is no geometry to answer with.</summary>
    public double ThumbCentreFromFraction(double fraction)
    {
        if (_remaining <= 0) return double.NaN;

        var along = Math.Clamp(fraction, 0, 1) * _remaining;
        if (Orientation == Orientation.Vertical && IsDirectionReversed) along = _remaining - along;
        return along + _thumbAlong / 2;
    }

    /// <summary>Converts a thumb-drag delta (in device pixels) into the corresponding change in <see cref="Value"/>.</summary>
    public double ValueFromDistance(double horizontal, double vertical)
    {
        if (Orientation == Orientation.Vertical)
            // Reversed (slider): dragging the thumb DOWN moves toward the minimum, so a downward delta DECREASES the value.
            return (IsDirectionReversed ? -vertical : vertical) * _density;
        return horizontal * _density;
    }

    /// <summary>Maps a point in the track's local space to the Value whose thumb would be CENTRED on it - used by a
    /// move-to-point slider click. Honours orientation and <see cref="IsDirectionReversed"/>.</summary>
    public double ValueFromPoint(Vector2 point)
    {
        if (_remaining <= 0) return Minimum;   // thumb fills the track - nowhere to move
        var pos = Orientation == Orientation.Vertical ? point.Y : point.X;
        var along = Math.Clamp(pos - _thumbAlong / 2, 0, _remaining);
        var offset = IsDirectionReversed ? _remaining - along : along;
        return Minimum + offset * _density;
    }
}
