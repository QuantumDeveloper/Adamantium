using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// Lays out the two thumbs of a <see cref="RangeSlider"/> and the band between them, and maps pixels to values for the
/// drag. The counterpart of <see cref="Track"/>, which positions a single thumb: the arithmetic is close but not the
/// same - here the travel of each thumb is the trough MINUS BOTH thumbs, so the two never overlap, and the band has to
/// follow whatever is left in between.
/// </summary>
public class RangeTrack : Panel
{
    private double _density;      // value units per pixel of travel
    private double _remaining;    // pixels a thumb can travel
    private double _thumbAlong;   // thumb size along the track

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(RangeTrack),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty MinimumProperty = AdamantiumProperty.Register(nameof(Minimum),
        typeof(double), typeof(RangeTrack), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty MaximumProperty = AdamantiumProperty.Register(nameof(Maximum),
        typeof(double), typeof(RangeTrack), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty LowerValueProperty = AdamantiumProperty.Register(nameof(LowerValue),
        typeof(double), typeof(RangeTrack), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty UpperValueProperty = AdamantiumProperty.Register(nameof(UpperValue),
        typeof(double), typeof(RangeTrack), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsArrange));

    /// <summary>The shortest the band between the thumbs is allowed to be drawn, in pixels. A VISUAL floor only: when the
    /// span is narrower than this the thumbs are pushed apart on screen, and the values are left alone - so a selection
    /// where both bounds sit on the same number still has something to grab.</summary>
    public static readonly AdamantiumProperty MinimumBandLengthProperty = AdamantiumProperty.Register(
        nameof(MinimumBandLength), typeof(double), typeof(RangeTrack),
        new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty LowerThumbProperty = AdamantiumProperty.Register(nameof(LowerThumb),
        typeof(Thumb), typeof(RangeTrack), new PropertyMetadata(null, OnPartChanged));

    public static readonly AdamantiumProperty UpperThumbProperty = AdamantiumProperty.Register(nameof(UpperThumb),
        typeof(Thumb), typeof(RangeTrack), new PropertyMetadata(null, OnPartChanged));

    /// <summary>The band between the thumbs. It is a Thumb itself, not a decoration: it shows the selected span AND is
    /// what the whole span is dragged by.</summary>
    public static readonly AdamantiumProperty CenterThumbProperty = AdamantiumProperty.Register(nameof(CenterThumb),
        typeof(Thumb), typeof(RangeTrack), new PropertyMetadata(null, OnPartChanged));

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

    public double LowerValue
    {
        get => GetValue<double>(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => GetValue<double>(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public double MinimumBandLength
    {
        get => GetValue<double>(MinimumBandLengthProperty);
        set => SetValue(MinimumBandLengthProperty, value);
    }

    public Thumb LowerThumb
    {
        get => GetValue<Thumb>(LowerThumbProperty);
        set => SetValue(LowerThumbProperty, value);
    }

    public Thumb UpperThumb
    {
        get => GetValue<Thumb>(UpperThumbProperty);
        set => SetValue(UpperThumbProperty, value);
    }

    public Thumb CenterThumb
    {
        get => GetValue<Thumb>(CenterThumbProperty);
        set => SetValue(CenterThumbProperty, value);
    }

    private static void OnPartChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RangeTrack track) return;
        if (e.OldValue is MeasurableUIComponent oldPart) track.Children.Remove(oldPart);
        if (e.NewValue is MeasurableUIComponent newPart) track.Children.Add(newPart);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(availableSize);

        // The track fills the trough; only the thumb's cross-size is an intrinsic demand.
        var thumb = (LowerThumb ?? UpperThumb)?.DesiredSize ?? default;
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
        var thumbDesired = (LowerThumb ?? UpperThumb)?.DesiredSize ?? default;
        var thumbAlong = vertical ? thumbDesired.Height : thumbDesired.Width;
        var thumbCross = Math.Min(vertical ? thumbDesired.Width : thumbDesired.Height, thickness);

        // What each thumb can travel: the trough minus BOTH thumbs (sizing it for one would let the pair drift past the
        // ends by half a thumb each) minus the band that always stands between them. Reserving the band here, once, is
        // what keeps a thumb under the pointer: the alternative - laying the thumbs out by value and prising them apart
        // when they get too close - makes the drag start with dead travel, because the pointer first has to undo that
        // prising before the thumb appears to move at all.
        var travel = Math.Max(0, trackLength - thumbAlong * 2 - MinimumBandLength);
        _density = travel > 0 && range > 0 ? range / travel : 0;
        _remaining = travel;
        _thumbAlong = thumbAlong;

        var lowerOffset = range > 0 ? Math.Clamp((LowerValue - Minimum) / range, 0, 1) * travel : 0;
        var upperOffset = range > 0 ? Math.Clamp((UpperValue - Minimum) / range, 0, 1) * travel : travel;
        if (upperOffset < lowerOffset) upperOffset = lowerOffset;

        // The upper thumb sits one thumb-width plus the band further along, since both occupy that space. With equal
        // values the two are therefore exactly one band apart on screen - a grabbable gap for a selection of one number.
        var lowerStart = lowerOffset;
        var upperStart = upperOffset + thumbAlong + MinimumBandLength;

        // The band runs BETWEEN the thumbs - from where the lower one ends to where the upper one starts - so grabbing it
        // can never mean grabbing an end thumb.
        var bandStart = lowerStart + thumbAlong;
        var bandLength = Math.Max(0, upperStart - bandStart);

        var crossOffset = Math.Max(0, (thickness - thumbCross) / 2);

        // The band has its own thickness (a thin bar between two fat handles), so it gets its OWN cross-centring. Giving
        // it the handles' cross-size instead left it sitting at the top edge of that box rather than on the rail.
        var bandDesired = CenterThumb?.DesiredSize ?? default;
        var bandCross = vertical ? bandDesired.Width : bandDesired.Height;
        if (bandCross <= 0 || bandCross > thickness) bandCross = Math.Min(thumbCross, thickness);
        var bandCrossOffset = Math.Max(0, (thickness - bandCross) / 2);

        if (vertical)
        {
            CenterThumb?.Arrange(new Rect(bandCrossOffset, bandStart, bandCross, bandLength));
            LowerThumb?.Arrange(new Rect(crossOffset, lowerStart, thumbCross, thumbAlong));
            UpperThumb?.Arrange(new Rect(crossOffset, upperStart, thumbCross, thumbAlong));
        }
        else
        {
            CenterThumb?.Arrange(new Rect(bandStart, bandCrossOffset, bandLength, bandCross));
            LowerThumb?.Arrange(new Rect(lowerStart, crossOffset, thumbAlong, thumbCross));
            UpperThumb?.Arrange(new Rect(upperStart, crossOffset, thumbAlong, thumbCross));
        }

        return finalSize;
    }

    /// <summary>Converts a thumb-drag delta (device pixels) into the change it makes to a value.</summary>
    public double ValueFromDistance(double horizontal, double vertical) =>
        (Orientation == Orientation.Vertical ? vertical : horizontal) * _density;

    /// <summary>The LOWER bound whose thumb would be centred on <paramref name="point"/> (track-local space).</summary>
    public double LowerValueFromPoint(Vector2 point) => ValueFromPoint(point, 0);

    /// <summary>The UPPER bound whose thumb would be centred on <paramref name="point"/>. Its lead-in differs from the
    /// lower one's by the lower thumb plus the reserved band, which both sit before it - mapping a point the same way for
    /// both is what made a click land on the wrong value.</summary>
    public double UpperValueFromPoint(Vector2 point) => ValueFromPoint(point, _thumbAlong + MinimumBandLength);

    private double ValueFromPoint(Vector2 point, double leadIn)
    {
        if (_remaining <= 0) return Minimum;

        var pos = Orientation == Orientation.Vertical ? point.Y : point.X;
        var along = Math.Clamp(pos - leadIn - _thumbAlong / 2, 0, _remaining);
        return Minimum + along * _density;
    }
}
