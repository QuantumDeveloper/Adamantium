using System;
using System.Collections.Generic;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Lays its children one after another along <see cref="Orientation"/>, each taking the length it declares - so many
/// pixels, or a weight in what is left over. Splits nest inside splits: that recursion is the docking layout.
/// <para>A Grid's RULE (fixed first, stars share the rest), deliberately not a Grid: a Grid keeps sizes in its
/// row/column definitions while the docking model keeps its own, and two copies of one number have to be synchronised
/// in both directions (drag a divider, load a layout, split a node) - every synchronisation being a place they drift
/// apart, and a save then reads whichever one lost. Here the length exists in ONE place and this panel only spends it.
/// <para>That was also the lesson: the first version kept a fraction AND a pixel hint, which is the very second copy it
/// set out to avoid, only inside its own panel. Every layout bug lived in the seam between the two.</para></para>
/// </summary>
public class PaneHost : Panel, IPaneMinimum
{
    /// <summary>Squeezing this host squeezes its children, so it answers with theirs: along its OWN axis the minimums
    /// add up (they sit end to end), across it the largest one wins (they all span it).</summary>
    public double MinimumExtent(Orientation orientation)
    {
        var along = orientation == Orientation;
        var total = 0.0;
        foreach (var child in Children)
        {
            if (child is PaneSplitter) continue;
            if (child is not IPaneMinimum owner) continue;

            var min = owner.MinimumExtent(orientation);
            if (along) total += min;
            else total = Math.Max(total, min);
        }

        if (along) total += Math.Max(0, ContentCount - 1) * DividerThickness;
        return total;
    }

    /// <summary>How much of the row this child takes - so many pixels, or a weight in the leftovers. Attached, so it
    /// travels with the child rather than sitting in a list on the parent: reordering children cannot hand one of them
    /// its neighbour's size.</summary>
    public static readonly AdamantiumProperty PaneLengthProperty =
        AdamantiumProperty.RegisterAttached("PaneLength", typeof(PaneLength), typeof(UIComponent),
            new PropertyMetadata(PaneLength.Star, LengthChanged));

    // A share is spent by the PARENT, so changing it has to re-arrange the parent - and that is done here, once, rather
    // than by every caller that writes a share (a splitter drag, a restored layout, a view-model). One road.
    // Not via AffectsParentArrange: that option is not implemented, so it silently did nothing - measured, with the
    // shares changing on every mouse move while the neighbour's width never moved off its starting value.
    private static void LengthChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        // MEASURE, not just arrange: this panel hands out sizes by share in BOTH passes, so a changed share that only
        // re-arranges leaves every child measured for the size it used to have and arranged into the size it now has.
        // A tab strip measured taller than it is given clips its headers, and the selection indicator - placed from the
        // strip's real bounds - ends up drawn through the text.
        if (component is IUIComponent child && child.VisualParent is PaneHost host) host.InvalidateMeasure();
    }


    /// <summary>Smallest share of the row a child may be squeezed to. Without a floor a drag can take a pane to nothing,
    /// and a pane of nothing has no edge left to grab - it can never be dragged back out. Settable (and themeable)
    /// rather than a constant: how small is "too small" depends on what the panes hold and how big the window is.
    /// <para>A pane's own <c>MinSize</c> wins over this whenever it asks for more - this is only the floor that applies
    /// when nothing else has an opinion.</para></summary>
    public static readonly AdamantiumProperty MinFractionProperty = AdamantiumProperty.Register(nameof(MinFraction),
        typeof(double), typeof(PaneHost), new PropertyMetadata(0.05));

    public double MinFraction
    {
        get => GetValue<double>(MinFractionProperty);
        set => SetValue(MinFractionProperty, value);
    }

    public static PaneLength GetPaneLength(IUIComponent element) => element.GetValue<PaneLength>(PaneLengthProperty);

    public static void SetPaneLength(IUIComponent element, PaneLength value) => element.SetValue(PaneLengthProperty, value);

    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(PaneHost),
        new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Thickness of the divider between two children. The dividers themselves are separate controls; this is
    /// the space the layout leaves for them.</summary>
    public static readonly AdamantiumProperty DividerThicknessProperty = AdamantiumProperty.Register(
        nameof(DividerThickness), typeof(double), typeof(PaneHost),
        new PropertyMetadata(4.0, PropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double DividerThickness
    {
        get => GetValue<double>(DividerThicknessProperty);
        set => SetValue(DividerThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var total = horizontal ? availableSize.Width : availableSize.Height;
        var across = horizontal ? availableSize.Height : availableSize.Width;

        var along = SpaceForChildren(total);

        // An Auto child is asked how much it needs BEFORE the space is handed out, because its answer is what it gets -
        // exactly as a Grid measures an Auto row first. Reading DesiredSize without this asks about the PREVIOUS pass,
        // so a pane that has just been collapsed keeps the height it had until something else forces another layout.
        foreach (var child in Children)
        {
            if (child is PaneSplitter || !GetPaneLength(child).IsAuto) continue;

            child.Measure(horizontal
                ? new Size(double.PositiveInfinity, across)
                : new Size(across, double.PositiveInfinity));
        }

        Distribute(along);

        var placed = 0;
        foreach (var child in Children)
        {
            // A splitter measures to the gap it will occupy, not to a share of the content.
            var size = child is PaneSplitter ? DividerThickness : _sizes[placed++];
            child.Measure(horizontal ? new Size(size, across) : new Size(across, size));
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var total = horizontal ? finalSize.Width : finalSize.Height;
        var along = SpaceForChildren(total);
        var across = horizontal ? finalSize.Height : finalSize.Width;

        var count = Distribute(along);
        // What the shares were spent over this pass - a splitter turns its pixel drag into a share against exactly this.
        _contentExtent = along;

        var offset = 0.0;
        var placed = 0;

        foreach (var child in Children)
        {
            if (child is PaneSplitter splitter)
            {
                // A splitter fills the gap that was reserved for it, and takes the host's direction so its drag knows
                // which way the boundary moves.
                splitter.Orientation = Orientation;
                splitter.Arrange(horizontal
                    ? new Rect(offset - DividerThickness, 0, DividerThickness, across)
                    : new Rect(0, offset - DividerThickness, across, DividerThickness));
                continue;
            }

            // The LAST content child takes what is left rather than its computed size: sizes are doubles, and rounding
            // each one separately leaves a hairline of background at the far edge. Measured against the FULL extent -
            // `offset` has the dividers already walked past baked into it.
            var size = ++placed == count ? Math.Max(0, total - offset) : _sizes[placed - 1];

            child.Arrange(horizontal
                ? new Rect(offset, 0, size, across)
                : new Rect(0, offset, across, size));

            offset += size + DividerThickness;
        }

        return finalSize;
    }

    /// <summary>How many children actually take a share. Splitters live in the gaps between them and are not content -
    /// counting them would hand each pane a slice of the space its own grip occupies.</summary>
    private int ContentCount
    {
        get
        {
            var count = 0;
            foreach (var child in Children)
            {
                if (child is not PaneSplitter) count++;
            }
            return count;
        }
    }

    private readonly List<IUIComponent> _content = [];
    private double[] _sizes = [];
    private double _contentExtent;

    /// <summary>Pixels the shares are currently spent over - the extent left after the dividers. A splitter needs it to
    /// turn a pixel drag into a share, and taking it from here rather than from the neighbours' bounds means the two
    /// agree by construction.</summary>
    internal double ContentExtent => _contentExtent;

    /// <summary>
    /// Fills <see cref="_sizes"/> with a pixel size per content child and returns how many there are. ONE rule, used by
    /// both passes - measuring by one rule and arranging by another is what leaves a tab strip measured for a height it
    /// is never given.
    /// <para>A Grid's rule, and for a Grid's reason: the fixed panes take their pixels off the top, and what remains is
    /// split between the starred ones by weight. So a docked inspector keeps the width it was given while the window
    /// resizes around it, and pulling a pane out of the row moves nobody but the stars.</para>
    /// </summary>
    private int Distribute(double along)
    {
        _content.Clear();
        foreach (var child in Children)
        {
            if (child is not PaneSplitter) _content.Add(child);
        }

        if (_sizes.Length < _content.Count) _sizes = new double[_content.Count];
        if (_content.Count == 0) return 0;

        // Every starred child must still be left something to stand in, or a large fixed pane could take the whole row
        // and the rest would have no edge left to grab.
        var floor = Math.Max(0, MinFraction) * along;
        var fixedTotal = 0.0;
        var starWeight = 0.0;
        var stars = 0;

        var horizontal = Orientation == Orientation.Horizontal;

        for (var i = 0; i < _content.Count; i++)
        {
            var length = GetPaneLength(_content[i]);
            if (length.IsPixel || length.IsAuto)
            {
                // Auto asks the child how much it needs - which is the whole point of it: a COLLAPSED pane is shrunk to
                // its own tab strip, and how tall a strip is is measured, never typed. Off the top like any other fixed
                // length, since a pane that says "only what I need" is not competing for the leftovers.
                var size = length.Value;
                if (length.IsAuto)
                {
                    var desired = _content[i] is IMeasurableComponent measurable ? measurable.DesiredSize : default;
                    size = horizontal ? desired.Width : desired.Height;
                }

                _sizes[i] = size;
                fixedTotal += size;
                continue;
            }

            _sizes[i] = double.NaN;
            stars++;
            starWeight += length.Value > 0 ? length.Value : 1;
        }

        // The fixed panes cannot have more than there is, minus standing room for the stars.
        var forFixed = Math.Max(0, along - stars * floor);
        if (fixedTotal > forFixed && fixedTotal > 0)
        {
            var scale = forFixed / fixedTotal;
            for (var i = 0; i < _content.Count; i++)
            {
                if (!double.IsNaN(_sizes[i])) _sizes[i] *= scale;
            }
            fixedTotal = forFixed;
        }

        var remaining = Math.Max(0, along - fixedTotal);
        for (var i = 0; i < _content.Count; i++)
        {
            if (!double.IsNaN(_sizes[i])) continue;

            var weight = GetPaneLength(_content[i]).Value;
            if (weight <= 0) weight = 1;
            _sizes[i] = starWeight > 0 ? remaining * weight / starWeight : remaining / stars;
        }

        EnforceMinimums(along);
        return _content.Count;
    }

    /// <summary>Holds every child at or above what it says it may shrink to (<see cref="IPaneMinimum"/>). A share knows
    /// nothing of minimums, so enough splits down one side drove a neighbour to a few pixels while it was asking for
    /// 200 - measured: the document column was handed 60 against a minimum of 200. Whoever is under its minimum is
    /// pinned AT it and the cost comes out of those still above theirs, in proportion to the slack each has.
    /// <para>Until now the minimums were a SPLITTER's business alone, so only a drag respected them - and every other
    /// way a size changes (a panel docked, a layout loaded, the window resized) walked straight past them.</para>
    /// <para>When the minimums do not all fit, everyone is scaled down together: there is no distribution that honours
    /// them, and refusing to lay out is not one of the options.</para></summary>
    private void EnforceMinimums(double along)
    {
        if (_minimums.Length < _content.Count) _minimums = new double[_content.Count];

        var needed = 0.0;
        for (var i = 0; i < _content.Count; i++)
        {
            _minimums[i] = _content[i] is IPaneMinimum owner ? Math.Max(0, owner.MinimumExtent(Orientation)) : 0;
            needed += _minimums[i];
        }

        if (needed <= 0) return;

        if (needed >= along)
        {
            var scale = along / needed;
            for (var i = 0; i < _content.Count; i++) _sizes[i] = _minimums[i] * scale;
            return;
        }

        // Paying for one child's minimum can push its payer under its own, so this repeats. Each pass pins at least one
        // more child, so it cannot run longer than there are children.
        for (var pass = 0; pass < _content.Count; pass++)
        {
            var deficit = 0.0;
            var slack = 0.0;
            for (var i = 0; i < _content.Count; i++)
            {
                if (_sizes[i] < _minimums[i]) deficit += _minimums[i] - _sizes[i];
                else slack += _sizes[i] - _minimums[i];
            }

            if (deficit <= Tolerance) return;

            for (var i = 0; i < _content.Count; i++)
            {
                if (_sizes[i] < _minimums[i])
                {
                    _sizes[i] = _minimums[i];
                    continue;
                }

                if (slack <= 0) continue;

                _sizes[i] -= deficit * (_sizes[i] - _minimums[i]) / slack;
            }
        }
    }

    private const double Tolerance = 0.01;
    private double[] _minimums = [];

    /// <summary>What this child is currently laid out at, in pixels along the row - what a drag starts from.</summary>
    internal double PixelsOf(IUIComponent child)
    {
        for (var i = 0; i < _content.Count; i++)
        {
            if (ReferenceEquals(_content[i], child)) return _sizes[i];
        }
        return 0;
    }

    private double SpaceForChildren(double total)
    {
        var dividers = Math.Max(0, ContentCount - 1) * DividerThickness;
        return Math.Max(0, total - dividers);
    }

}
