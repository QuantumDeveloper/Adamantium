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
    private readonly Border[] _edges = new Border[4];

    // The order the sides are stored in, and the only place that order is written down.
    private static readonly DockZone[] Zones =
        [DockZone.Center, DockZone.Left, DockZone.Top, DockZone.Right, DockZone.Bottom];

    /// <summary>The four EDGE anchors, in the same order the sides above are - "along that whole side of the area",
    /// as opposed to the cross, which is about the one group under the pointer.</summary>
    private static readonly DockZone[] EdgeZones =
        [DockZone.Left, DockZone.Top, DockZone.Right, DockZone.Bottom];

    public DockCompass()
    {
        IsHitTestVisible = false;   // it is a read-out of a gesture in progress, never a thing to click
        Children.Add(_preview);

        for (var i = 0; i < _indicators.Length; i++)
        {
            _indicators[i] = new Border();
            Children.Add(_indicators[i]);
        }

        for (var i = 0; i < _edges.Length; i++)
        {
            _edges[i] = new Border();
            Children.Add(_edges[i]);
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

    /// <summary>How far the edge anchors sit from the area's own edge. Not flush against it: a target touching the
    /// screen edge is easy to overshoot, and a little inset also says it belongs to the AREA rather than to whatever
    /// the pointer happens to be over.</summary>
    public static readonly AdamantiumProperty EdgeIndicatorInsetProperty = AdamantiumProperty.Register(
        nameof(EdgeIndicatorInset), typeof(double), typeof(DockCompass),
        new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsArrange));

    /// <summary>Width of the outline around each indicator. A target floating over arbitrary content needs an EDGE to be
    /// aimed at; without one there is only a translucent fill, which over a light background is nothing at all.</summary>
    public static readonly AdamantiumProperty IndicatorStrokeThicknessProperty = AdamantiumProperty.Register(
        nameof(IndicatorStrokeThickness), typeof(double), typeof(DockCompass),
        new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsArrange));

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

    public double IndicatorStrokeThickness
    {
        get => GetValue<double>(IndicatorStrokeThicknessProperty);
        set => SetValue(IndicatorStrokeThicknessProperty, value);
    }

    public double EdgeIndicatorInset
    {
        get => GetValue<double>(EdgeIndicatorInsetProperty);
        set => SetValue(EdgeIndicatorInsetProperty, value);
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
    private bool _armedIsEdge;
    private double _edgeExtent = double.NaN;

    /// <summary>Aims at the group under the pointer - its rectangle in THIS control's own coordinates - and lights up the
    /// indicator the pointer is on, or <see cref="DockZone.None"/> between them.
    /// <para>This control covers the whole docking area, in one overlay window that neither moves nor resizes for the
    /// length of a drag. That is what makes the rectangle safe to pass: it is a sub-rectangle of a surface that is not
    /// changing under it. Sizing the window to the GROUP instead meant the rectangle and the surface were two separate
    /// updates racing each other - measured, the overlay had already resized to the next group while the compass was
    /// still laid out at the previous size, so the cross was built around a centre it was nowhere near.</para></summary>
    /// <param name="isEdge">The armed indicator is one of the four EDGE anchors rather than one of the cross - which is
    /// what decides whether the preview covers part of the group or part of the whole area.</param>
    /// <param name="edgeExtent">How wide the band an edge anchor would take is, so the preview shows what the drop does.</param>
    public void AimAt(Rect group, DockZone armed, bool isEdge = false, double edgeExtent = double.NaN)
    {
        if (_group == group && _armed == armed && _armedIsEdge == isEdge && _edgeExtent.Equals(edgeExtent)) return;

        _group = group;
        _armed = armed;
        _armedIsEdge = isEdge;
        _edgeExtent = edgeExtent;
        InvalidateArrange();
    }

    /// <summary>Nothing is aimed at - draw neither the indicators nor the preview.</summary>
    public void Clear() => AimAt(default, DockZone.None);

    /// <summary>Which zones the panes being dragged are ALLOWED to land in - the rest are not drawn at all.
    /// <para>Not drawn rather than drawn-and-inert: an indicator is a promise that dropping there does something, and one
    /// that quietly declines is worse than none - you aim at it, nothing happens, and there is no way to tell a refusal
    /// from a missed target. What CANNOT be shown this way is the application's veto (DockingArea.PaneDocking), which is
    /// asked on the drop: the compass shows what the ZONES allow, which is data, and a veto is an exception to it.</para>
    /// <para>Deliberately NOT an AdamantiumProperty, unlike the brushes and sizes beside it. Those are looks and belong to
    /// the theme; this is the state of the gesture in progress, like the aimed-at rectangle - it comes from the panes
    /// being dragged and nobody else has an opinion worth having. Made settable from markup, a theme could say "All" and
    /// get indicators that decline, since the real check lives in DockingArea.Resolve.</para>
    /// </summary>
    public DockZone AllowedZones
    {
        get => _allowed;
        set
        {
            if (_allowed == value) return;

            _allowed = value;
            InvalidateArrange();
        }
    }

    private DockZone _allowed = DockZone.All;

    /// <summary>Which EDGE anchors may be drawn. Separate from <see cref="AllowedZones"/> because the two answer
    /// different questions with the same four bits: Left in the cross means "split the panel under the pointer", which
    /// that panel pays for, while Left on the rim means "a band down the whole side", which the DOCUMENT AREA pays for.
    /// One mask for both drew rim anchors the centre had no room left for - aim, drop, nothing happens.</summary>
    public DockZone AllowedEdgeZones
    {
        get => _allowedEdges;
        set
        {
            if (_allowedEdges == value) return;

            _allowedEdges = value;
            InvalidateArrange();
        }
    }

    private DockZone _allowedEdges = DockZone.All;

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

    /// <summary>Which EDGE anchor a point falls on for an area occupying <paramref name="area"/>, or
    /// <see cref="DockZone.None"/>. Asked before <see cref="ZoneAt"/>: the edges belong to the area and the cross to
    /// whichever group is under the pointer, and an edge anchor is the more specific answer where they meet.</summary>
    public static DockZone EdgeZoneAt(Rect area, Vector2 point, double indicatorSize, double inset)
    {
        for (var i = 0; i < EdgeZones.Length; i++)
        {
            var slot = EdgeSlotOf(EdgeZones[i], area, indicatorSize, inset);
            if (point.X >= slot.X && point.X <= slot.X + slot.Width &&
                point.Y >= slot.Y && point.Y <= slot.Y + slot.Height)
            {
                return EdgeZones[i];
            }
        }

        return DockZone.None;
    }

    /// <summary>Where an edge anchor sits: centred on its side of the area, inset from it.</summary>
    private static Rect EdgeSlotOf(DockZone zone, Rect area, double size, double inset)
    {
        var cx = area.X + area.Width / 2 - size / 2;
        var cy = area.Y + area.Height / 2 - size / 2;

        return zone switch
        {
            DockZone.Left => new Rect(area.X + inset, cy, size, size),
            DockZone.Right => new Rect(area.X + area.Width - inset - size, cy, size, size),
            DockZone.Top => new Rect(cx, area.Y + inset, size, size),
            _ => new Rect(cx, area.Y + area.Height - inset - size, size, size)
        };
    }

    /// <summary>Where a pane dropped in <paramref name="zone"/> would end up inside <paramref name="target"/>. A side
    /// takes half by default; the centre joins the tabs and so covers the whole group.
    /// <para><paramref name="extent"/> overrides how much the newcomer takes along the split axis - an EDGE anchor is a
    /// side panel, a band of a couple of hundred pixels, not half the editor. Same function for the preview and for what
    /// the drop then does, so the two cannot disagree.</para></summary>
    public static Rect PreviewOf(Rect target, DockZone zone, double extent)
    {
        if (double.IsNaN(extent) || extent <= 0) return PreviewOf(target, zone);

        var width = Math.Min(extent, target.Width);
        var height = Math.Min(extent, target.Height);

        return zone switch
        {
            DockZone.Left => new Rect(target.X, target.Y, width, target.Height),
            DockZone.Right => new Rect(target.X + target.Width - width, target.Y, width, target.Height),
            DockZone.Top => new Rect(target.X, target.Y, target.Width, height),
            DockZone.Bottom => new Rect(target.X, target.Y + target.Height - height, target.Width, height),
            _ => target
        };
    }

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

    protected override Size ArrangeOverride(Size finalSize)
    {

        var area = new Rect(0, 0, finalSize.Width, finalSize.Height);
        var aiming = _group.Width > 0 && _group.Height > 0;
        var size = IndicatorSize;

        // An edge anchor takes half of the WHOLE area, a cross target half of the group - the preview says which of the
        // two was aimed at, so the same rectangle is previewed and then occupied.
        _preview.Background = PreviewBrush;
        _preview.Visibility = aiming && _armed != DockZone.None ? Visibility.Visible : Visibility.Collapsed;
        _preview.Arrange(_armedIsEdge ? PreviewOf(area, _armed, _edgeExtent) : PreviewOf(_group, _armed));

        // The cross sits at the centre of the group - the same centre ZoneAt measures its indicators from, so what is
        // drawn and what is hit are one arrangement.
        var cx = _group.X + _group.Width / 2;
        var cy = _group.Y + _group.Height / 2;
        var step = size + IndicatorGap;

        for (var i = 0; i < _indicators.Length; i++)
        {
            var indicator = _indicators[i];
            indicator.Visibility = aiming && (_allowed & Zones[i]) != 0 ? Visibility.Visible : Visibility.Collapsed;
            indicator.Background = !_armedIsEdge && Zones[i] == _armed ? ActiveBrush : IndicatorBrush;
            indicator.BorderBrush = IndicatorStroke;
            // Without a thickness the brush above draws nothing: Border's default is zero, so the outline that separates
            // a target from whatever it floats over was never there at all.
            indicator.BorderThickness = new Thickness(IndicatorStrokeThickness);
            indicator.Arrange(SlotOf(Zones[i], cx, cy, step, size));
        }

        // The edge anchors belong to the AREA, so they are placed from finalSize and stay where they are whichever group
        // the pointer wanders over. Shown whenever the compass is up at all - they are aimable even between groups.
        var inset = EdgeIndicatorInset;
        for (var i = 0; i < _edges.Length; i++)
        {
            var edge = _edges[i];
            edge.Visibility = aiming && (_allowedEdges & EdgeZones[i]) != 0 ? Visibility.Visible : Visibility.Collapsed;
            edge.Background = _armedIsEdge && EdgeZones[i] == _armed ? ActiveBrush : IndicatorBrush;
            edge.BorderBrush = IndicatorStroke;
            edge.BorderThickness = new Thickness(IndicatorStrokeThickness);
            edge.Arrange(EdgeSlotOf(EdgeZones[i], area, size, inset));
        }

        return finalSize;
    }
}
