using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// The five indicators shown over the group under the pointer - centre for "another tab here", four sides for "split
/// it" - plus the translucent preview of where the pane would land.
/// <para>The decision is made by hitting an INDICATOR, not by which quarter of the group the pointer is in. A quarter
/// map has no edges the user can see, so the only way to find out what a drop will do is to do it; five targets can be
/// aimed at, and the preview says what each one means before the button comes up.</para>
/// <para>Geometry lives in <see cref="ZoneAt"/> and is pure arithmetic over the target rectangle, so what the compass
/// shows and what a drop does are one calculation asked twice - see <see cref="DockTarget"/>.</para>
/// </summary>
public class DockCompass : Panel
{
    private readonly Border _preview = new();
    private readonly Border[] _indicators = new Border[5];

    // The order the sides are stored in, and the only place that order is written down.
    private static readonly DockZone[] Zones =
        [DockZone.Center, DockZone.Left, DockZone.Top, DockZone.Right, DockZone.Bottom];

    public DockCompass()
    {
        IsHitTestVisible = false;   // it is a read-out of a gesture in progress, never a thing to click
        Children.Add(_preview);

        for (var i = 0; i < _indicators.Length; i++)
        {
            _indicators[i] = new Border();
            Children.Add(_indicators[i]);
        }
    }

    /// <summary>Size of one indicator. A property rather than a constant: how big a target has to be to aim at
    /// comfortably depends on the pointer, the screen and the theme.</summary>
    public static readonly AdamantiumProperty IndicatorSizeProperty = AdamantiumProperty.Register(
        nameof(IndicatorSize), typeof(double), typeof(DockCompass),
        new PropertyMetadata(34.0, PropertyMetadataOptions.AffectsArrange));

    /// <summary>Gap between the centre indicator and the four around it.</summary>
    public static readonly AdamantiumProperty IndicatorGapProperty = AdamantiumProperty.Register(
        nameof(IndicatorGap), typeof(double), typeof(DockCompass),
        new PropertyMetadata(6.0, PropertyMetadataOptions.AffectsArrange));

    public static readonly AdamantiumProperty IndicatorBrushProperty = AdamantiumProperty.Register(
        nameof(IndicatorBrush), typeof(Brush), typeof(DockCompass), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IndicatorStrokeProperty = AdamantiumProperty.Register(
        nameof(IndicatorStroke), typeof(Brush), typeof(DockCompass), new PropertyMetadata(null));

    /// <summary>Fill of the indicator the pointer is on. This is the only feedback saying WHICH of the five is armed.</summary>
    public static readonly AdamantiumProperty ActiveBrushProperty = AdamantiumProperty.Register(
        nameof(ActiveBrush), typeof(Brush), typeof(DockCompass), new PropertyMetadata(null));

    public double IndicatorSize
    {
        get => GetValue<double>(IndicatorSizeProperty);
        set => SetValue(IndicatorSizeProperty, value);
    }

    public double IndicatorGap
    {
        get => GetValue<double>(IndicatorGapProperty);
        set => SetValue(IndicatorGapProperty, value);
    }

    public Brush IndicatorBrush
    {
        get => GetValue<Brush>(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public Brush IndicatorStroke
    {
        get => GetValue<Brush>(IndicatorStrokeProperty);
        set => SetValue(IndicatorStrokeProperty, value);
    }

    public Brush ActiveBrush
    {
        get => GetValue<Brush>(ActiveBrushProperty);
        set => SetValue(ActiveBrushProperty, value);
    }

    /// <summary>Fill of the preview rectangle. Translucent on purpose: it covers the content it describes, and an opaque
    /// one would hide the very thing being aimed at.</summary>
    public static readonly AdamantiumProperty PreviewBrushProperty = AdamantiumProperty.Register(
        nameof(PreviewBrush), typeof(Brush), typeof(DockCompass), new PropertyMetadata(null));

    public Brush PreviewBrush
    {
        get => GetValue<Brush>(PreviewBrushProperty);
        set => SetValue(PreviewBrushProperty, value);
    }

    private DockZone _armed = DockZone.None;
    private Rect _group;

    /// <summary>Aims at the group under the pointer - its rectangle in THIS control's own coordinates - and lights up the
    /// indicator the pointer is on, or <see cref="DockZone.None"/> between them.
    /// <para>This control covers the whole docking area, in one overlay window that neither moves nor resizes for the
    /// length of a drag. That is what makes the rectangle safe to pass: it is a sub-rectangle of a surface that is not
    /// changing under it. Sizing the window to the GROUP instead meant the rectangle and the surface were two separate
    /// updates racing each other - measured, the overlay had already resized to the next group while the compass was
    /// still laid out at the previous size, so the cross was built around a centre it was nowhere near.</para></summary>
    public void AimAt(Rect group, DockZone armed)
    {
        if (_group == group && _armed == armed) return;

        _group = group;
        _armed = armed;
        InvalidateArrange();
    }

    /// <summary>Nothing is aimed at - draw neither the indicators nor the preview.</summary>
    public void Clear() => AimAt(default, DockZone.None);

    /// <summary>Which indicator a point falls on for a group occupying <paramref name="target"/>, or
    /// <see cref="DockZone.None"/>. Static and pure: this is what the drop asks too, so the two can never disagree.</summary>
    public static DockZone ZoneAt(Rect target, Vector2 point, double indicatorSize, double gap)
    {
        var cx = target.X + target.Width / 2;
        var cy = target.Y + target.Height / 2;
        var step = indicatorSize + gap;

        for (var i = 0; i < Zones.Length; i++)
        {
            var slot = SlotOf(Zones[i], cx, cy, step, indicatorSize);
            if (point.X >= slot.X && point.X <= slot.X + slot.Width &&
                point.Y >= slot.Y && point.Y <= slot.Y + slot.Height)
            {
                return Zones[i];
            }
        }

        return DockZone.None;
    }

    /// <summary>Where a pane dropped in <paramref name="zone"/> would end up inside <paramref name="target"/>. A side
    /// takes half; the centre joins the tabs and so covers the whole group.</summary>
    public static Rect PreviewOf(Rect target, DockZone zone)
    {
        var halfWidth = target.Width / 2;
        var halfHeight = target.Height / 2;

        return zone switch
        {
            DockZone.Left => new Rect(target.X, target.Y, halfWidth, target.Height),
            DockZone.Right => new Rect(target.X + halfWidth, target.Y, halfWidth, target.Height),
            DockZone.Top => new Rect(target.X, target.Y, target.Width, halfHeight),
            DockZone.Bottom => new Rect(target.X, target.Y + halfHeight, target.Width, halfHeight),
            _ => target
        };
    }

    private static Rect SlotOf(DockZone zone, double cx, double cy, double step, double size)
    {
        var half = size / 2;
        var (dx, dy) = zone switch
        {
            DockZone.Left => (-step, 0.0),
            DockZone.Right => (step, 0.0),
            DockZone.Top => (0.0, -step),
            DockZone.Bottom => (0.0, step),
            _ => (0.0, 0.0)
        };

        return new Rect(cx + dx - half, cy + dy - half, size, size);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(availableSize);
        return availableSize;
    }

    // Whether this control is laid out at all, and whether its style ever reached it - the two things that decide if
    // anything appears. Printed from INSIDE the layout pass, which is the only place that can tell "not yet" from "never".
    private void LogArrange(Size finalSize)
    {
        System.Console.WriteLine($"[DockCompass] arrange=({finalSize.Width:F0}x{finalSize.Height:F0}) " +
                                 $"group=({_group.X:F0},{_group.Y:F0} {_group.Width:F0}x{_group.Height:F0}) armed={_armed} " +
                                 $"indicatorBrush={IndicatorBrush != null} activeBrush={ActiveBrush != null} " +
                                 $"previewBrush={PreviewBrush != null} visibility={Visibility}");
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (DockingArea.LogDocking) LogArrange(finalSize);

        var aiming = _group.Width > 0 && _group.Height > 0;

        _preview.Background = PreviewBrush;
        _preview.Visibility = aiming && _armed != DockZone.None ? Visibility.Visible : Visibility.Collapsed;
        _preview.Arrange(PreviewOf(_group, _armed));

        // The cross sits at the centre of the group - the same centre ZoneAt measures its indicators from, so what is
        // drawn and what is hit are one arrangement.
        var cx = _group.X + _group.Width / 2;
        var cy = _group.Y + _group.Height / 2;
        var size = IndicatorSize;
        var step = size + IndicatorGap;

        for (var i = 0; i < _indicators.Length; i++)
        {
            var indicator = _indicators[i];
            indicator.Visibility = aiming ? Visibility.Visible : Visibility.Collapsed;
            indicator.Background = Zones[i] == _armed ? ActiveBrush : IndicatorBrush;
            indicator.BorderBrush = IndicatorStroke;
            indicator.Arrange(SlotOf(Zones[i], cx, cy, step, size));
        }

        return finalSize;
    }
}
