using System;
using System.Collections.Generic;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Lays its children one after another along <see cref="Orientation"/>, each taking the SHARE it declares. Splits nest
/// inside splits - that recursion is the docking layout.
/// <para>Deliberately not a <c>Grid</c> with splitters: a Grid keeps sizes in its row/column definitions while the
/// docking model keeps fractions, and two copies of one number have to be synchronised in both directions (drag a
/// divider, load a layout, split a node) - every synchronisation being a place they drift apart, and a save then reads
/// whichever one lost. Here the fraction exists in ONE place, and this panel only spends it.</para>
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

    /// <summary>Share of the split this child takes, 0..1. Attached, so it travels with the child rather than sitting
    /// in a list on the parent - reordering children cannot hand one of them its neighbour's size.</summary>
    public static readonly AdamantiumProperty FractionProperty =
        AdamantiumProperty.RegisterAttached("Fraction", typeof(double), typeof(UIComponent),
            new PropertyMetadata(double.NaN, FractionChanged));

    // A share is spent by the PARENT, so changing it has to re-arrange the parent - and that is done here, once, rather
    // than by every caller that writes a share (a splitter drag, a restored layout, a view-model). One road.
    // Not via AffectsParentArrange: that option is not implemented, so it silently did nothing - measured, with the
    // shares changing on every mouse move while the neighbour's width never moved off its starting value.
    private static void FractionChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        // MEASURE, not just arrange: this panel hands out sizes by share in BOTH passes, so a changed share that only
        // re-arranges leaves every child measured for the size it used to have and arranged into the size it now has.
        // A tab strip measured taller than it is given clips its headers, and the selection indicator - placed from the
        // strip's real bounds - ends up drawn through the text.
        if (component is IUIComponent child && child.VisualParent is PaneHost host) host.InvalidateMeasure();
    }

    /// <summary>Starting size in PIXELS, or NaN. The author writes "the inspector starts about 240 wide" without knowing
    /// the window size, and this stays true through every layout pass rather than being converted to a share at some
    /// moment guessed to be final - there is no such moment, and converting against a provisional extent bakes in
    /// whatever size the window happened to have at that instant. The first splitter drag freezes it into a share
    /// (<see cref="FreezeShares"/>) and from then on the share is the truth, since a pixel number would be a lie the
    /// moment a boundary moves.</summary>
    public static readonly AdamantiumProperty DesiredSizeHintProperty =
        AdamantiumProperty.RegisterAttached("DesiredSizeHint", typeof(double), typeof(UIComponent),
            new PropertyMetadata(double.NaN));

    public static double GetDesiredSizeHint(IUIComponent element) => element.GetValue<double>(DesiredSizeHintProperty);

    public static void SetDesiredSizeHint(IUIComponent element, double value) => element.SetValue(DesiredSizeHintProperty, value);

    /// <summary>Smallest share a child may be squeezed to. Without a floor a drag can take a pane to nothing, and a
    /// pane of nothing has no edge left to grab - it can never be dragged back out. Settable (and themeable) rather
    /// than a constant: how small is "too small" depends on what the panes hold and how big the window is.
    /// <para>A pane's own <c>MinSize</c> wins over this whenever it asks for more - this is only the floor that applies
    /// when nothing else has an opinion.</para></summary>
    public static readonly AdamantiumProperty MinFractionProperty = AdamantiumProperty.Register(nameof(MinFraction),
        typeof(double), typeof(PaneHost), new PropertyMetadata(0.05));

    public double MinFraction
    {
        get => GetValue<double>(MinFractionProperty);
        set => SetValue(MinFractionProperty, value);
    }

    public static double GetFraction(IUIComponent element) => element.GetValue<double>(FractionProperty);

    public static void SetFraction(IUIComponent element, double value) => element.SetValue(FractionProperty, value);

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

        if (LogLayout) Log(along);
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
    /// <para>A pixel hint is an ABSOLUTE claim taken off the top; whatever is left is divided between the children that
    /// made no claim, in proportion to their own shares. So "the console starts 160 tall" means 160, not "160 as a
    /// share of a size nobody knows yet" - and, crucially, a child without a hint still gets the share it was
    /// given, instead of an equal slice that ignores it.</para>
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

        // Every child that made no claim must still be left something to stand in, or a large hint could take the whole
        // extent and the rest would have no edge left to grab.
        var floor = Math.Max(0, MinFraction) * along;
        var claimed = 0.0;
        var freeWeight = 0.0;
        var free = 0;
        var weighted = true;

        for (var i = 0; i < _content.Count; i++)
        {
            var hint = GetDesiredSizeHint(_content[i]);
            if (!double.IsNaN(hint) && hint > 0)
            {
                _sizes[i] = hint;
                claimed += hint;
                continue;
            }

            _sizes[i] = double.NaN;
            free++;

            var fraction = GetFraction(_content[i]);
            if (double.IsNaN(fraction) || fraction <= 0) weighted = false;
            else freeWeight += fraction;
        }

        var forClaims = Math.Max(0, along - free * floor);
        if (claimed > forClaims && claimed > 0)
        {
            var scale = forClaims / claimed;
            for (var i = 0; i < _content.Count; i++)
            {
                if (!double.IsNaN(_sizes[i])) _sizes[i] *= scale;
            }
            claimed = forClaims;
        }

        var remaining = Math.Max(0, along - claimed);
        for (var i = 0; i < _content.Count; i++)
        {
            if (!double.IsNaN(_sizes[i])) continue;
            // Proportional only when every claimless child actually has a share to be proportional to; otherwise one of
            // them would be weighted against nothing and come out at zero.
            _sizes[i] = weighted && freeWeight > 0
                ? remaining * GetFraction(_content[i]) / freeWeight
                : remaining / free;
        }

        return _content.Count;
    }

    /// <summary>Turns what is on screen RIGHT NOW into the shares that reproduce it, and drops every pixel hint in this
    /// host.
    /// <para>Called once when a drag STARTS. Without it the first mouse move would change the basis underneath the
    /// pointer - the dragged pair lose their hints while their siblings keep theirs, so every size in the host is
    /// suddenly derived from a different rule and the boundary jumps away from the cursor before it has moved a pixel.
    /// Freezing first costs nothing visually (these shares lay out to exactly the same pixels) and leaves the drag doing
    /// arithmetic on one kind of number.</para></summary>
    internal void FreezeShares()
    {
        var count = Distribute(_contentExtent);
        if (count == 0 || _contentExtent <= 0) return;

        var total = 0.0;
        for (var i = 0; i < count; i++) total += _sizes[i];
        if (total <= 0) return;

        for (var i = 0; i < count; i++)
        {
            SetDesiredSizeHint(_content[i], double.NaN);
            SetFraction(_content[i], _sizes[i] / total);
        }
    }

    private double SpaceForChildren(double total)
    {
        var dividers = Math.Max(0, ContentCount - 1) * DividerThickness;
        return Math.Max(0, total - dividers);
    }

    /// <summary>Set ADAMANTIUM_DOCK_LOG=1 to have every host print what it handed out. Off by default: this runs on
    /// every layout pass.</summary>
    internal static readonly bool LogLayout = Environment.GetEnvironmentVariable("ADAMANTIUM_DOCK_LOG") == "1";

    private void Log(double along)
    {
        var text = $"[PaneHost {Orientation} along={along:F1}]";
        for (var i = 0; i < _content.Count; i++)
        {
            var child = _content[i];
            var name = child is IName named && !string.IsNullOrEmpty(named.Name) ? named.Name : child.GetType().Name;
            var got = Orientation == Orientation.Horizontal ? child.Bounds.Width : child.Bounds.Height;
            var min = child is IPaneMinimum owner ? owner.MinimumExtent(Orientation) : 0;
            text += $" {name}(hint={GetDesiredSizeHint(child):F1} frac={GetFraction(child):F3} -> gave {_sizes[i]:F1} got {got:F1} min {min:F1})";
        }
        Console.WriteLine(text);
    }
}
