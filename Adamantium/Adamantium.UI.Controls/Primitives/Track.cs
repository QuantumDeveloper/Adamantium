using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Primitives;

/// <summary>
/// The interactive area of a <see cref="ScrollBar"/> (and, later, a Slider): a draggable <see cref="Thumb"/> flanked
/// by two invisible <see cref="RepeatButton"/>s that page towards/away from the thumb. It sizes and positions those
/// three parts from <see cref="Minimum"/>/<see cref="Maximum"/>/<see cref="Value"/>/<see cref="ViewportSize"/>, with
/// no template of its own (the parts are created in code). Mirrors WPF's Track.
/// </summary>
public class Track : Panel
{
    // The thumb never shrinks below this along the track, so it stays grabbable even with a huge scroll range.
    private const double MinThumbLength = 12.0;

    private double _density;   // value units per pixel of thumb travel (for ValueFromDistance)

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

    public Track()
    {
        DecreaseRepeatButton = CreatePageButton();
        Thumb = new Thumb();
        IncreaseRepeatButton = CreatePageButton();

        // Paint order: page areas under the thumb (the thumb sits on top and stays grabbable).
        Children.Add(DecreaseRepeatButton);
        Children.Add(IncreaseRepeatButton);
        Children.Add(Thumb);
    }

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

    public Thumb Thumb { get; }

    /// <summary>Pages towards the maximum (below/right of the thumb).</summary>
    public RepeatButton IncreaseRepeatButton { get; }

    /// <summary>Pages towards the minimum (above/left of the thumb).</summary>
    public RepeatButton DecreaseRepeatButton { get; }

    private static RepeatButton CreatePageButton()
    {
        // A ScrollBarPageButton (not a plain RepeatButton) so the default RepeatButton chrome doesn't apply: invisible
        // (no template -> renders nothing) but hit-testable by its arranged bounds, so a press on the trough pages and
        // auto-repeats. Not focusable so paging never steals focus from the scrolled content.
        return new ScrollBarPageButton { Background = null, Focusable = false };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(availableSize);

        // The track stretches to fill the scrollbar's trough; the thumb's cross-size is the only intrinsic demand.
        var thumb = Thumb.DesiredSize;
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

        // Thumb length: proportional to the viewport fraction for a scrollbar; a fixed minimum when there is no
        // viewport (slider-style); the whole track when there is nothing to scroll.
        double thumbLength;
        if (range <= 0)
            thumbLength = trackLength;
        else if (viewport > 0)
            thumbLength = trackLength * viewport / (range + viewport);
        else
            thumbLength = MinThumbLength;
        thumbLength = Math.Clamp(thumbLength, Math.Min(MinThumbLength, trackLength), trackLength);

        var remaining = trackLength - thumbLength;
        var offset = range > 0 ? (Value - Minimum) / range * remaining : 0;
        offset = Math.Clamp(offset, 0, remaining);

        // Thumb travel in value-units-per-pixel, used by the thumb-drag mapping (ValueFromDistance).
        _density = remaining > 0 && range > 0 ? range / remaining : 0;

        var increaseStart = offset + thumbLength;
        var increaseLength = Math.Max(0, trackLength - increaseStart);

        if (vertical)
        {
            DecreaseRepeatButton.Arrange(new Rect(0, 0, thickness, offset));
            Thumb.Arrange(new Rect(0, offset, thickness, thumbLength));
            IncreaseRepeatButton.Arrange(new Rect(0, increaseStart, thickness, increaseLength));
        }
        else
        {
            DecreaseRepeatButton.Arrange(new Rect(0, 0, offset, thickness));
            Thumb.Arrange(new Rect(offset, 0, thumbLength, thickness));
            IncreaseRepeatButton.Arrange(new Rect(increaseStart, 0, increaseLength, thickness));
        }

        return finalSize;
    }

    /// <summary>Converts a thumb-drag delta (in device pixels) into the corresponding change in <see cref="Value"/>.</summary>
    public double ValueFromDistance(double horizontal, double vertical)
    {
        var delta = Orientation == Orientation.Vertical ? vertical : horizontal;
        return delta * _density;
    }
}
