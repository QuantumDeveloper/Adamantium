using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Panels;

/// <summary>The items host of the ribbon's STRIP: tab headers in a row, and over any run of them belonging to one
/// contextual group, that group's ledge (docs/RIBBON_PLAN.md §4.2).
/// <para>Not a <see cref="TabPanel"/>, which was only ever standing in until the ledges arrived. The ledges are part of
/// the PANEL rather than a layer above it for the same reason a focus ring lives in the layer of what it decorates:
/// they have to travel and to be clipped with the tabs they describe, and a layer on top can do neither.</para></summary>
public class RibbonTabPanel : Panel
{
    /// <summary>Height of the ledge row. ZERO while no group is active: an ordinary ribbon must not stand taller
    /// because of a possibility nobody is using.</summary>
    public static readonly AdamantiumProperty LedgeHeightProperty = AdamantiumProperty.Register(nameof(LedgeHeight),
        typeof(double), typeof(RibbonTabPanel),
        new PropertyMetadata(18.0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

    public double LedgeHeight
    {
        get => GetValue<double>(LedgeHeightProperty);
        set => SetValue(LedgeHeightProperty, value);
    }

    // The ledges are OURS, not items: they are added straight to the visual tree, so the items presenter's own
    // Children.Clear() never touches them and the container generator never sees them.
    private readonly List<RibbonContextualLedge> _ledges = [];

    // The headers in the order they are LAID OUT, which is not the order they were authored in - see Order().
    private readonly List<IMeasurableComponent> _order = [];

    // (first header index, last header index, group) for each run of neighbouring tabs sharing a group.
    private readonly List<(int First, int Last, RibbonContextualGroup Group)> _runs = [];

    /// <summary>Left/Right walk the headers as they STAND, ledges skipped - the ledge is a label, and the laid-out
    /// order is not the authored one once contextual tabs are in the strip.</summary>
    public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
    {
        if (!IsArrow(direction)) return base.Navigate(from, direction);
        if (IsVertical(direction)) return null;   // a ROW: Up/Down are not "the next tab"

        var at = _order.IndexOf(from as IMeasurableComponent);
        if (at < 0) return null;

        var next = at + (IsForward(direction) ? 1 : -1);
        return next >= 0 && next < _order.Count ? _order[next] : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Order();
        Repartition();
        SyncLedges();

        var ledgeRow = LedgeRow;

        // Unbounded along the row so each header takes exactly its own width; the ledge row is not theirs to fill.
        var forHeaders = new Size(double.PositiveInfinity, Math.Max(0, availableSize.Height - ledgeRow));

        double width = 0, height = 0;
        foreach (var header in _order)
        {
            header.Measure(forHeaders);
            width += header.DesiredSize.Width;
            height = Math.Max(height, header.DesiredSize.Height);
        }

        foreach (var ledge in _ledges)
        {
            ledge.Measure(new Size(double.PositiveInfinity, height + ledgeRow));
        }

        width += Widen();
        return new Size(width, height + ledgeRow);
    }

    // Extra width handed to each header, so its run is at least as wide as its ledge's title.
    private readonly Dictionary<IMeasurableComponent, double> _extra = [];

    // Where each plate was last put, so a shape change can be told to re-record itself (see ArrangeOverride).
    private readonly Dictionary<RibbonContextualLedge, Rect> _placed = [];

    /// <summary>A ledge is exactly as wide as the run it names, and a run can be one narrow tab - "LIGHT TOOLS" over
    /// "Light". Neither answer at the edges is acceptable: a ledge widened past its tabs lies about which tabs it
    /// covers, and a clipped title is a title nobody can read. Office solves it the third way - it WIDENS THE TABS
    /// until the title fits - so the ledge still spans exactly its run, and nothing is cut. Returns the width added.</summary>
    private double Widen()
    {
        _extra.Clear();
        double added = 0;

        for (var i = 0; i < _runs.Count && i < _ledges.Count; i++)
        {
            var (first, last, _) = _runs[i];

            double run = 0;
            for (var at = first; at <= last; at++) run += _order[at].DesiredSize.Width;

            var shortfall = _ledges[i].DesiredSize.Width - run;
            if (shortfall <= 0) continue;

            // Spread evenly, so a run of equal tabs stays equal rather than growing one of them into a slab.
            var each = shortfall / (last - first + 1);
            for (var at = first; at <= last; at++) _extra[_order[at]] = each;

            added += shortfall;
        }

        return added;
    }

    private double WidthOf(IMeasurableComponent header) =>
        header.DesiredSize.Width + (_extra.TryGetValue(header, out var extra) ? extra : 0);

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Read from the headers HERE rather than from a number the last measure left behind: an arrange can follow a
        // measure that ran under a different shape (the ledge switched off between them). Clamped to the slot, never
        // stretched to it: a strip occupies only what it stacks.
        double tallest = 0;
        foreach (var header in _order) tallest = Math.Max(tallest, header.DesiredSize.Height);

        var row = Math.Min(finalSize.Height, tallest);

        // The TABS come first and the band takes what is left. The strip grows by the band's height when a context
        // appears, but the slot only catches up on the NEXT pass - and taking the band out of the tabs' height in the
        // meantime squashed them to a third of their size for that frame.
        var ledgeRow = Math.Max(0, Math.Min(LedgeRow, finalSize.Height - row));

        // Where each header starts and ends, so a ledge can be laid over the run it describes.
        var edges = new double[_order.Count + 1];
        double x = 0;
        for (var i = 0; i < _order.Count; i++)
        {
            var header = _order[i];
            var width = WidthOf(header);
            edges[i] = x;
            header.Arrange(new Rect(x, ledgeRow, width, row));
            x += width;
        }

        edges[_order.Count] = x;

        // The plate is the TITLE BAND over its run; the tabs below carry their own colour, and stand as tabs rather
        // than as holes cut in a slab.
        for (var i = 0; i < _runs.Count && i < _ledges.Count; i++)
        {
            var (first, last, _) = _runs[i];
            var slot = new Rect(edges[first], 0, edges[last + 1] - edges[first], ledgeRow);
            var ledge = _ledges[i];

            // A plate is a raw visual child, outside Children and outside the items presenter, so nothing marks it for
            // re-recording when it merely CHANGES SHAPE - and a plate given zero height went on being drawn at the
            // height it had. Told here, and only when the shape actually moved, so a still strip records nothing.
            if (!_placed.TryGetValue(ledge, out var before) || before != slot)
            {
                _placed[ledge] = slot;
                ledge.InvalidateRender(true);
            }

            ledge.Arrange(slot);
        }

        return new Size(x, row + ledgeRow);
    }

    private double _headerRow;

    // The title band costs height only if some context actually asks for a title; a strip of plates alone is exactly as
    // tall as it always was.
    private double LedgeRow
    {
        get
        {
            foreach (var run in _runs)
            {
                if (run.Group.ShowHeader) return LedgeHeight;
            }

            return 0;
        }
    }

    // Ordinary tabs first, in the order their author wrote them; then the contextual ones, group by group, oldest
    // activation first. The AUTHOR's list is never touched - it is his - so the order lives here, in the layout.
    private void Order()
    {
        _order.Clear();

        foreach (var child in Children)
        {
            if (Shown(child) && GroupOf(child) == null) _order.Add(child);
        }

        var contextual = new List<(IMeasurableComponent Header, long Activated, int Authored)>();
        var at = 0;
        foreach (var child in Children)
        {
            if (Shown(child) && GroupOf(child) != null) contextual.Add((child, GroupOf(child).ActivatedAt, at));
            at++;
        }

        // Groups by when they became active; WITHIN a group the author's own order. The authored index is part of the
        // key on purpose: List.Sort is not stable, so comparing activation alone would let a group's tabs shuffle among
        // themselves - and would let two groups interleave whenever their stamps tie.
        contextual.Sort((a, b) =>
        {
            var byGroup = a.Activated.CompareTo(b.Activated);
            return byGroup != 0 ? byGroup : a.Authored.CompareTo(b.Authored);
        });

        foreach (var entry in contextual) _order.Add(entry.Header);
    }

    // A run is a MAXIMAL stretch of neighbouring headers with the SAME group. Neighbouring, not merely belonging: an
    // ordinary tab placed between two tabs of one group yields TWO ledges with the same title, and that is the honest
    // answer - the strip shows what is in it. Office forbids the arrangement instead; we make it predictable.
    private void Repartition()
    {
        _runs.Clear();

        var i = 0;
        while (i < _order.Count)
        {
            var group = GroupOf(_order[i]);
            if (group == null)
            {
                i++;
                continue;
            }

            var last = i;
            while (last + 1 < _order.Count && ReferenceEquals(GroupOf(_order[last + 1]), group)) last++;

            // EVERY run gets a plate - that is what colours its tabs. Only a run whose group asks for a title costs the
            // strip the title band's height.
            _runs.Add((i, last, group));
            i = last + 1;
        }
    }

    // One ledge per run. Kept as a pool rather than rebuilt: the runs change on every activation, and a fresh visual
    // each time would throw away its themed template for nothing.
    private void SyncLedges()
    {
        while (_ledges.Count < _runs.Count)
        {
            var ledge = new RibbonContextualLedge();
            _ledges.Add(ledge);
            AddVisualChild(ledge);
            Theme(ledge);
        }

        while (_ledges.Count > _runs.Count)
        {
            var last = _ledges[^1];
            _ledges.RemoveAt(_ledges.Count - 1);
            RemoveVisualChild(last);
        }

        for (var i = 0; i < _runs.Count; i++)
        {
            var group = _runs[i].Group;
            _ledges[i].Content = group.ShowHeader ? group.Header : null;
            _ledges[i].Accent = group.Accent;
            _ledges[i].TitleHeight = group.ShowHeader ? LedgeHeight : 0;
        }
    }

    /// <summary>A ledge is framework CHROME, not an item: it is joined to the visual tree only, so nothing gives it a
    /// logical parent - and it is the logical parent's setter that applies the theme. Left alone it would have no
    /// template at all and would take up its row while drawing nothing. So it is themed OUT OF BAND, exactly as an
    /// adorner and a template root are: the inheritance parent for its DataContext, the theme applied by hand.</summary>
    private void Theme(RibbonContextualLedge ledge)
    {
        ledge.InheritanceParent = this;
        ledge.ApplyCurrentTheme();
    }

    /// <summary>Out-of-band theming means out-of-band RE-theming too: a theme swap reaches this panel, and the ledges
    /// hang off it rather than under it.</summary>
    protected override void ApplyCurrentThemeCore()
    {
        base.ApplyCurrentThemeCore();

        foreach (var ledge in _ledges) Theme(ledge);
    }

    private static bool Shown(IMeasurableComponent child) => child.Visibility == Visibility.Visible;

    private static RibbonContextualGroup GroupOf(IMeasurableComponent child) =>
        (child as RibbonTabHeader)?.ContextualGroup;
}
