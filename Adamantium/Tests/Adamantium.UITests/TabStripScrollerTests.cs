using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Reproduces the tab-strip regression: after the ItemsPresenter was wrapped in a TabStripScroller, only the first tab
// showed. The scroller measures the strip with an unbounded main axis so the (virtualizing) StackPanel realizes ALL
// items (it can pan a fully-realized strip). This checks that realization actually happens.
public class TabStripScrollerTests
{
    private static ItemsControl StripControl(int count)
    {
        var ic = new ItemsControl
        {
            ItemsSource = Enumerable.Range(0, count).Cast<object>().ToList(),
            ItemTemplate = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Width = 80, Height = 30 } }),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new StackPanel { Orientation = Orientation.Horizontal }
            })
        };
        ic.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var scroller = new TabStripScroller { Orientation = Orientation.Horizontal, Child = presenter };
            var result = new TemplateResult { RootComponent = scroller };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        return ic;
    }

    [Test]
    public void RealizesAllTabs_WhenViewportFitsThem()
    {
        var ic = StripControl(8);              // 8 * 80 = 640
        ic.Measure(new Size(1000, 100));       // fits
        ic.Arrange(new Rect(0, 0, 1000, 100));

        Assert.That(ic.ItemContainerGenerator.RealizedCount, Is.EqualTo(8));
    }

    [Test]
    public void RealizedTabs_AreVisualChildrenOfTheStrip_WithNonZeroBounds()
    {
        var ic = StripControl(8);
        ic.Measure(new Size(1000, 100));
        ic.Arrange(new Rect(0, 0, 1000, 100));

        var panel = ic.ItemsHostPanel;
        Assert.That(panel, Is.Not.Null, "the strip panel exists");
        var children = panel.VisualChildren.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(children, Has.Count.EqualTo(8), "every realized container is a visual child of the strip panel");
            foreach (var c in children.Cast<IMeasurableComponent>())
                Assert.That(c.RenderSize.Width, Is.GreaterThan(0), "each tab container is arranged with a real width");
        });
    }

    [Test]
    public void OwnContainerItems_AttachToTheStrip_AndRender()
    {
        // Authored TabItems are "item is its own container" (IUIComponent items), the real TabControl case - unlike the
        // data-item + ItemTemplate path above. Add Borders directly as items so they are their own containers.
        var ic = new ItemsControl
        {
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new StackPanel { Orientation = Orientation.Horizontal }
            })
        };
        for (var i = 0; i < 8; i++) ic.Items.Add(new Border { Width = 80, Height = 30 });
        ic.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var scroller = new TabStripScroller { Orientation = Orientation.Horizontal, Child = presenter };
            var result = new TemplateResult { RootComponent = scroller };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        ic.Measure(new Size(1000, 100));
        ic.Arrange(new Rect(0, 0, 1000, 100));

        var children = ic.ItemsHostPanel.VisualChildren.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(children, Has.Count.EqualTo(8), "authored (own-container) items attach to the strip");
            foreach (var c in children.Cast<IMeasurableComponent>())
                Assert.That(c.RenderSize.Width, Is.GreaterThan(0), "each own-container tab is arranged with a real width");
        });
    }

    // The TabControl template stacks the strip (Auto row) over the content (* row) in a Grid. This checks that Grid does
    // separate Grid.Row=0/1 into non-overlapping rows (the user saw the tabs vanish under the content = rows not applied).
    [Test]
    public void Grid_AutoStarRows_StackChildrenWithoutOverlap()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var top = new Border { Height = 40 };
        var bottom = new Border();
        Grid.SetRow(top, 0);
        Grid.SetRow(bottom, 1);
        grid.Children.Add(top);
        grid.Children.Add(bottom);

        grid.Measure(new Size(200, 300));
        grid.Arrange(new Rect(0, 0, 200, 300));

        Assert.Multiple(() =>
        {
            Assert.That(top.Bounds.Y, Is.EqualTo(0).Within(0.5), "Auto row 0 sits at the top");
            Assert.That(top.Bounds.Height, Is.EqualTo(40).Within(0.5), "Auto row is content height");
            Assert.That(bottom.Bounds.Y, Is.EqualTo(40).Within(0.5), "* row 1 starts BELOW row 0 (no overlap)");
            Assert.That(bottom.Bounds.Height, Is.EqualTo(260).Within(0.5), "* row fills the rest");
        });
    }

    // Exactly what the AUML string form <Grid RowDefinitions="Auto,*"> generates INSIDE a ControlTemplate: a by-name
    // SetValue at Template priority. RowDefinitions is a plain CLR property (not a dependency property), so the by-name
    // SetValue used to silently drop it (no rows -> tabs and content overlapped). This is the real regression.
    [Test]
    public void Grid_RowDefinitions_StringForm_AppliesRows()
    {
        var grid = new Grid();
        grid.SetValue("RowDefinitions",
            Adamantium.Core.TypeParsing.TypeParser.Parse<RowDefinitions>("Auto, *"), ValuePriority.Template);
        Assert.That(grid.RowDefinitions.Count, Is.EqualTo(2), "by-name SetValue on the CLR property still applies the rows");

        var top = new Border { Height = 40 };
        var bottom = new Border();
        Grid.SetRow(top, 0);
        Grid.SetRow(bottom, 1);
        grid.Children.Add(top);
        grid.Children.Add(bottom);
        grid.Measure(new Size(200, 300));
        grid.Arrange(new Rect(0, 0, 200, 300));

        Assert.That(bottom.Bounds.Y, Is.EqualTo(40).Within(0.5),
            "content sits below the strip - the assigned-via-setter rows are actually used");
    }

    [Test]
    public void RealizesAllTabs_EvenWhenViewportIsNarrow()
    {
        var ic = StripControl(8);              // 640 wide
        ic.Measure(new Size(200, 100));        // narrower than the strip -> overflows (pannable), but ALL still realized
        ic.Arrange(new Rect(0, 0, 200, 100));

        Assert.That(ic.ItemContainerGenerator.RealizedCount, Is.EqualTo(8));
    }

    // Builds a TabControl whose strip is a content-sized TabPanel, realized + arranged so every tab has real Bounds -
    // the setup the drag logic reads. Tabs get explicit widths so positions are deterministic.
    private static (TabControl tc, TabItem[] tabs) ArrangedStrip(params double[] widths)
    {
        var tc = new TabControl();
        var tabs = widths.Select(w => new TabItem { Width = w, Height = 24 }).ToArray();
        foreach (var t in tabs) tc.Items.Add(t);
        tc.ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
        {
            RootComponent = new TabPanel { Orientation = Orientation.Horizontal }
        });
        tc.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        tc.Measure(new Size(1000, 100));
        tc.Arrange(new Rect(0, 0, 1000, 100));
        return (tc, tabs);
    }

    // The reorder must land the dragged tab where it was dropped - a regression guard for "the dragged item flew to the
    // end of the strip". Drag tab 0 just past tab 1's centre: it should commit to index 1, not the end.
    [Test]
    public void Drag_LandsAtDroppedPosition_NotFlungToTheEnd()
    {
        var (tc, tabs) = ArrangedStrip(40, 100, 60, 80);   // slots: 0, 40, 140, 200
        Assert.Multiple(() =>
        {
            Assert.That(tabs[0].Bounds.X, Is.EqualTo(0).Within(0.5), "content-sized cumulative layout");
            Assert.That(tabs[1].Bounds.X, Is.EqualTo(40).Within(0.5));
            Assert.That(tabs[2].Bounds.X, Is.EqualTo(140).Within(0.5));
        });

        tc.BeginDrag(tabs[0], 5.0);      // grab 5px into tab 0
        tc.UpdateDrag(tabs[0], 100.0);   // dragged centre = 115 -> past tab1 centre (90), before tab2 centre (170)
        tc.EndDrag(tabs[0]);
        AnimationManager.Tick(10);       // finish the settle -> the reorder commits

        Assert.Multiple(() =>
        {
            Assert.That(tc.Items.IndexOf(tabs[0]), Is.EqualTo(1), "landed one slot right, NOT flung to the end");
            Assert.That(tc.Items.IndexOf(tabs[1]), Is.EqualTo(0), "the passed neighbour shifted into the vacated slot");
            Assert.That(tc.Items.IndexOf(tabs[3]), Is.EqualTo(3), "the far tab did not move");
        });

        // The items-host panel's children must reorder to match (the moved tab in slot 1, NOT appended to the end): this
        // is where the "flew to the end" bug lived - Children.Insert appended the moved tab. Children order drives the
        // arrange in a real layout pass, so it's the harness-independent proof the reorder is correct.
        var children = tc.ItemsHostPanel.Children.Cast<object>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(children[0], Is.SameAs(tabs[1]), "neighbour shifted into slot 0");
            Assert.That(children[1], Is.SameAs(tabs[0]), "the moved tab sits in slot 1, NOT appended to the end");
            Assert.That(children[2], Is.SameAs(tabs[2]));
            Assert.That(children[3], Is.SameAs(tabs[3]));
        });
    }

    // A short drag that never passes a neighbour's centre must slide home (commit nothing).
    [Test]
    public void Drag_ShortOfMidpoint_SlidesHome_NoReorder()
    {
        var (tc, tabs) = ArrangedStrip(40, 100, 60, 80);

        tc.BeginDrag(tabs[0], 5.0);
        tc.UpdateDrag(tabs[0], 30.0);    // centre = 45 -> short of tab1 centre (90)
        tc.EndDrag(tabs[0]);
        AnimationManager.Tick(10);

        Assert.That(tc.Items.IndexOf(tabs[0]), Is.EqualTo(0), "no midpoint crossed -> order unchanged");
    }

    // The tab strip's panel (TabPanel) sizes each tab to its OWN content and lays them out cumulatively - NOT the single
    // uniform extent the virtualizing StackPanel would give (which made headers of differing width all take the probe
    // tab's width, and a drag-reorder that moved a different tab into the probe slot resized them all).
    [Test]
    public void TabPanel_SizesEachChildToItsContent_LaidOutCumulatively()
    {
        var panel = new TabPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Border { Width = 40, Height = 20 });
        panel.Children.Add(new Border { Width = 100, Height = 20 });
        panel.Children.Add(new Border { Width = 60, Height = 20 });
        panel.Measure(new Size(1000, 100));
        panel.Arrange(new Rect(0, 0, 1000, 100));

        var tabs = panel.Children.Cast<IMeasurableComponent>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(tabs[0].Bounds.Width, Is.EqualTo(40).Within(0.5), "each tab keeps its own width");
            Assert.That(tabs[1].Bounds.Width, Is.EqualTo(100).Within(0.5));
            Assert.That(tabs[2].Bounds.Width, Is.EqualTo(60).Within(0.5));
            Assert.That(tabs[1].Bounds.X, Is.EqualTo(40).Within(0.5), "positioned cumulatively at content widths");
            Assert.That(tabs[2].Bounds.X, Is.EqualTo(140).Within(0.5));
        });
    }

    // The overflow ▾ button binds its visibility to the scroller's scroll state, so that state must be correct: a strip
    // wider than the viewport reports CanScrollForward (content past the end) at rest, and CanScrollBack only once panned.
    [Test]
    public void Scroller_ReportsOverflow_ThenBack_AfterPanning()
    {
        var scroller = new TabStripScroller
        {
            Orientation = Orientation.Horizontal,
            Child = new Border { Width = 800, Height = 30 }
        };
        scroller.Measure(new Size(300, 30));       // viewport 300 < 800 extent -> overflows
        scroller.Arrange(new Rect(0, 0, 300, 30));

        Assert.Multiple(() =>
        {
            Assert.That(scroller.CanScrollForward, Is.True, "800 extent > 300 viewport -> more content past the end");
            Assert.That(scroller.CanScrollBack, Is.False, "at rest (offset 0) nothing is hidden before the start");
        });

        scroller.LineScroll(+1);                   // pan toward the end
        scroller.Measure(new Size(300, 30));
        scroller.Arrange(new Rect(0, 0, 300, 30));
        Assert.That(scroller.CanScrollBack, Is.True, "panned off the start -> content is now hidden before the viewport");
    }

    // The off-screen-until-resize bug: the ▾ overflow button is now OVERLAID on the strip (a single-cell grid, right-
    // aligned) and starts Collapsed; when shown it must re-lay-out flush inside the right edge, never past it. This drives
    // the real LayoutManager loop (ExecuteLayoutPass) - NOT a bare grid.Measure, which early-returns on the unchanged root
    // constraint: a child's InvalidateMeasure enqueues the child in the manager, and only the loop drains it and propagates
    // the re-measure up to the grid (MeasureDirty -> parent.InvalidateMeasure on a size change). A resize "fixed" the button
    // only because it fed the root a NEW constraint, forcing a fresh pass; the loop makes it reflow with no resize.
    [Test]
    public void OverflowButton_RelaysOutInBounds_AfterBecomingVisible_ViaLayoutPass()
    {
        var grid = new Grid();
        var strip = new Border { Height = 30 };                                  // fills the cell (the scroller stand-in)
        var overflow = new Border
        {
            Width = 28, Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed
        };
        grid.Children.Add(strip);
        grid.Children.Add(overflow);

        grid.Measure(new Size(300, 30));
        grid.Arrange(new Rect(0, 0, 300, 30));
        Assert.That(overflow.Bounds.Width, Is.EqualTo(0).Within(0.5), "collapsed -> not laid out");

        overflow.Visibility = Visibility.Visible;          // enqueues the button in its LayoutManager
        LayoutManager.For(grid).ExecuteLayoutPass();       // one frame of layout - the loop the running app uses

        Assert.Multiple(() =>
        {
            Assert.That(overflow.Bounds.Width, Is.EqualTo(28).Within(0.5), "shown -> re-measured to its width");
            Assert.That(overflow.Bounds.Right, Is.LessThanOrEqualTo(300 + 0.5), "stays inside the right edge (the off-screen regression)");
            Assert.That(overflow.Bounds.X, Is.EqualTo(272).Within(0.5), "pinned flush to the right edge");
        });
    }

}
