using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// An area showing several <see cref="Pane"/>s as tabs - the LEAF of a docking layout. Splits nest; groups never do.
/// <para>A <see cref="TabControl"/> already is this: tabs, reordering along the strip, and tearing a tab off into its
/// own window all work there and were built for exactly this. A group adds only where it sits.</para>
/// </summary>
public class PaneGroup : TabControl, Panels.IPaneMinimum
{
    /// <summary>How small this group may become along one axis: the largest <see cref="Pane.MinSize"/> among its panes,
    /// and never less than its own tab strip needs - an area shorter than its strip has its headers clipped, and there
    /// is nothing left to grab or click. The answer comes from the panes' own policy, not from a number invented here.
    /// </summary>
    public double MinimumExtent(Panels.Orientation orientation)
    {
        var min = MinSizeAppliesAlong(orientation) ? LargestPaneMinimum() : 0;
        return System.Math.Max(min, StripExtent(orientation));
    }

    /// <summary>A pane's <see cref="Pane.MinSize"/> is its smallest useful size ALONG THE AXIS IT IS DOCKED ON, so it
    /// only answers for that axis. "The inspector is never narrower than 180" is a statement about width; letting it
    /// answer for height too meant the inspector's width forbade the console below it from being dragged taller - a
    /// limit nobody wrote and nothing explains. A group in the centre has no single axis, so its panes speak for
    /// both.</summary>
    private bool MinSizeAppliesAlong(Panels.Orientation orientation)
    {
        return Zone switch
        {
            DockZone.Left or DockZone.Right => orientation == Panels.Orientation.Horizontal,
            DockZone.Top or DockZone.Bottom => orientation == Panels.Orientation.Vertical,
            _ => true
        };
    }

    private double LargestPaneMinimum()
    {
        var min = 0.0;
        for (var i = 0; i < Items.Count; i++)
        {
            // Authored panes ARE the items; bound ones are reached through their container.
            if (Items[i] is Pane authored) min = System.Math.Max(min, authored.MinSize);
            else if (ItemContainerGenerator.ContainerFromIndex(i) is Pane generated) min = System.Math.Max(min, generated.MinSize);
        }
        return min;
    }

    /// <summary>What the tab strip needs - measured, not assumed - and only along the axis it actually stacks against.
    /// A strip across the top is a floor on HEIGHT; it says nothing about how narrow the group may be.</summary>
    private double StripExtent(Panels.Orientation orientation)
    {
        if (ItemsHostPanel is not { } panel) return 0;

        return TabStripPlacement is TabStripPlacement.Left or TabStripPlacement.Right
            ? orientation == Panels.Orientation.Horizontal ? panel.DesiredSize.Width : 0
            : orientation == Panels.Orientation.Vertical ? panel.DesiredSize.Height : 0;
    }

    public PaneGroup()
    {
        // A torn-off pane is the docking area's business, not the application's: it becomes another root of the same
        // layout. Without a handler the strip would simply put the tab back, which is exactly what it did before.
        TabTornOff += (_, e) =>
        {
            if (Area is not { } area || e.Container is not Pane pane) return;
            e.Handled = area.TearOff(pane, e);
        };
    }

    // Where each tab actually ended up, and what render offset it still carries. Overlapping tabs mean either two slots
    // at one position or a leftover drag transform, and only the numbers tell which.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (DockingArea.LogDocking)
        {
            // Template identity next to panel identity: a NEW panel with the SAME template means the rebuild came from
            // somewhere other than a template change; a new template means the theme handed out a fresh instance.
            var text = $"[PaneGroup #{GetHashCode()} {Items.Count} tabs host=#{ItemsHostPanel?.GetHashCode()} " +
                       $"tmpl=#{Template?.GetHashCode()} parent={VisualParent?.GetType().Name}]";
            for (var i = 0; i < Items.Count; i++)
            {
                if (ItemContainerGenerator.ContainerFromIndex(i) is not TabItem tab) continue;

                var offset = tab.RenderTransform is Core.Media.Transform t ? t.TranslateX : 0;
                // Is this tab actually IN the strip's panel? A container that never got re-attached keeps drawing at the
                // bounds of its previous life - which looks exactly like an overlap and is nothing of the kind.
                var panel = ItemsHostPanel as Panels.Panel;
                var inChildren = panel != null && panel.Children.Contains(tab);
                text += $" [{i}] x={tab.Bounds.X:F0} w={tab.Bounds.Width:F0} desired={tab.DesiredSize.Width:F0} " +
                        $"dx={offset:F0} inChildren={inChildren} vis={tab.Visibility}";
            }
            System.Console.WriteLine(text);
        }

        return size;
    }

    /// <summary>The docking area this group lives in, or null when it is used as a plain tab control.</summary>
    private DockingArea Area
    {
        get
        {
            for (var parent = VisualParent; parent != null; parent = parent.VisualParent)
            {
                if (parent is DockingArea area) return area;
            }
            return null;
        }
    }

    /// <summary>Where the AUTHOR put this group. This is the whole vocabulary of markup - a zone, not a share of a
    /// split the author cannot see. <see cref="DockingArea"/> builds the split tree from these.</summary>
    public static readonly AdamantiumProperty ZoneProperty = AdamantiumProperty.Register(nameof(Zone),
        typeof(DockZone), typeof(PaneGroup), new PropertyMetadata(DockZone.Center));

    /// <summary>Starting size in PIXELS along the zone's axis, or NaN. A hint for the first layout only: the author
    /// says "the inspector starts about 220 wide" without knowing the window size, and the first arrange turns it into
    /// a fraction. After that the fraction is the truth - a pixel number would be a lie the moment a divider moves.</summary>
    public static readonly AdamantiumProperty SizeProperty = AdamantiumProperty.Register(nameof(Size),
        typeof(double), typeof(PaneGroup), new PropertyMetadata(double.NaN));

    public DockZone Zone
    {
        get => GetValue<DockZone>(ZoneProperty);
        set => SetValue(ZoneProperty, value);
    }

    public double Size
    {
        get => GetValue<double>(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
