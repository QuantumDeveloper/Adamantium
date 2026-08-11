using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The strip's own panel (docs/RIBBON_PLAN.md §4.2): headers in a row, and over any run of NEIGHBOURING tabs
/// sharing a contextual group, that group's ledge. It replaces the TabPanel the strip borrowed until the ledges
/// existed, so it also has to keep what that one did - above all sizing headers to their content rather than to the
/// slot.</summary>
[TestFixture]
public class RibbonTabPanelTests
{
    private const double LedgeHeight = 18;

    private static RibbonTabHeader Header(double width, RibbonContextualGroup group = null)
    {
        var header = new RibbonTabHeader { Width = width, Height = 24, ContextualGroup = group };
        return header;
    }

    private static RibbonTabPanel Strip(params RibbonTabHeader[] headers)
    {
        var panel = new RibbonTabPanel { LedgeHeight = LedgeHeight };
        foreach (var header in headers) panel.Children.Add(header);
        return panel;
    }

    private static void LayoutAt(RibbonTabPanel panel, double width, double height = 60)
    {
        ((IMeasurableComponent)panel).Measure(new Size(width, height));
        ((IMeasurableComponent)panel).Arrange(new Rect(0, 0, width, height));
    }

    // Titleless by default: a ledge WIDENS its run until its title fits (see the widening tests), so a test about
    // anything else states no title and the runs keep the widths it set.
    private static RibbonContextualGroup Group(string header = "") =>
        new() { Header = header, IsActive = true };

    // The ledge row costs nothing until a context exists: an ordinary ribbon must not stand taller for a possibility
    // nobody uses.
    [Test]
    public void WithNoContextTheStripIsJustItsHeaders()
    {
        var panel = Strip(Header(80), Header(90));
        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(panel.DesiredSize.Height, Is.EqualTo(24));
            Assert.That(panel.Children[0].Bounds.Y, Is.Zero);
        });
    }

    [Test]
    public void AContextRaisesTheStripByOneLedge()
    {
        var mesh = Group();
        var panel = Strip(Header(80), Header(90, mesh));
        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(panel.DesiredSize.Height, Is.EqualTo(24 + LedgeHeight));
            Assert.That(panel.Children[0].Bounds.Y, Is.EqualTo(LedgeHeight), "the headers sit under the ledge row");
        });
    }

    // The strip occupies only what it stacks - honest bounds, exactly what TabPanel did. Asserted on the PANEL, because
    // a header with a stated Height would report that height whatever slot it was handed, and prove nothing.
    [Test]
    public void TheStripTakesOnlyTheHeightOfItsHeaders()
    {
        var panel = Strip(Header(80));
        LayoutAt(panel, 400, height: 200);

        Assert.That(panel.Bounds.Height, Is.EqualTo(24), "not the 200 it was offered");
    }

    // Contextual tabs stand AFTER the ordinary ones whatever order they were authored in, and the ledge is laid over
    // the run rather than over one tab.
    [Test]
    public void ContextualTabsGoToTheEndAndShareOneLedge()
    {
        var mesh = Group();
        var a = Header(50, mesh);
        var ordinary = Header(70);
        var b = Header(60, mesh);
        var panel = Strip(a, ordinary, b);

        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(ordinary.Bounds.X, Is.Zero, "the ordinary tab comes first");
            Assert.That(a.Bounds.X, Is.EqualTo(70));
            Assert.That(b.Bounds.X, Is.EqualTo(120), "and the group's tabs are brought together");
            Assert.That(Ledges(panel), Has.Count.EqualTo(1), "one run, one ledge");
            // The ledge is the TITLE BAND over its run; the tabs below carry their own colour and stand as tabs.
            Assert.That(Ledges(panel)[0].Bounds, Is.EqualTo(new Rect(70, 0, 110, LedgeHeight)));
        });
    }

    // Two groups, two ledges, oldest activation first - tabs that appeared LAST stand furthest right and do not shift
    // what someone was already aiming at.
    [Test]
    public void GroupsStandInTheOrderTheyBecameActive()
    {
        var first = Group();
        var second = Group();
        first.ActivatedAt = 1;
        second.ActivatedAt = 2;

        var late = Header(60, second);
        var early = Header(50, first);
        var panel = Strip(late, early);

        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(early.Bounds.X, Is.Zero);
            Assert.That(late.Bounds.X, Is.EqualTo(50));
            Assert.That(Ledges(panel), Has.Count.EqualTo(2));
        });
    }

    // A hidden tab (its group is off) is not in the strip at all - and takes its ledge with it.
    [Test]
    public void AnInactiveGroupsTabsAreNotInTheRow()
    {
        var mesh = Group();
        var hidden = Header(50, mesh);
        hidden.Visibility = Visibility.Collapsed;
        var panel = Strip(Header(80), hidden);

        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(panel.DesiredSize.Width, Is.EqualTo(80));
            Assert.That(panel.DesiredSize.Height, Is.EqualTo(24), "and the ledge row is gone with it");
            Assert.That(Ledges(panel), Is.Empty);
        });
    }

    // The arrows walk the strip AS LAID OUT, which is not the authored order once a context is in it; the ledge is a
    // label and is never a destination.
    [Test]
    public void TheArrowsWalkTheRowAsItStands()
    {
        var mesh = Group();
        var contextual = Header(50, mesh);
        var ordinary = Header(70);
        var panel = Strip(contextual, ordinary);
        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(panel.Navigate(ordinary, FocusNavigationDirection.Right), Is.SameAs(contextual));
            Assert.That(panel.Navigate(contextual, FocusNavigationDirection.Left), Is.SameAs(ordinary));
            Assert.That(panel.Navigate(contextual, FocusNavigationDirection.Right), Is.Null, "the end of the row");
            Assert.That(panel.Navigate(ordinary, FocusNavigationDirection.Down), Is.Null, "a ROW answers nothing across");
        });
    }

    // Two contexts are two ledges, never one plate spanning both.
    [Test]
    public void TwoContextsAreTwoLedges()
    {
        var mesh = Group();
        var light = Group();
        mesh.ActivatedAt = 1;
        light.ActivatedAt = 2;

        var panel = Strip(Header(50, mesh), Header(60, light));
        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(Ledges(panel), Has.Count.EqualTo(2));
            Assert.That(Ledges(panel)[0].Bounds.Width, Is.EqualTo(50));
            Assert.That(Ledges(panel)[1].Bounds.Width, Is.EqualTo(60));
        });
    }

    // The author's order is kept WITHIN a group. List.Sort is not stable, so ordering by activation alone let a group's
    // own tabs shuffle among themselves.
    [Test]
    public void AGroupsTabsKeepTheOrderTheyWereWrittenIn()
    {
        var mesh = Group();
        mesh.ActivatedAt = 1;

        var geometry = Header(50, mesh);
        var uv = Header(60, mesh);
        var materials = Header(70, mesh);
        var panel = Strip(geometry, uv, materials);

        LayoutAt(panel, 400);

        Assert.Multiple(() =>
        {
            Assert.That(geometry.Bounds.X, Is.Zero);
            Assert.That(uv.Bounds.X, Is.EqualTo(50));
            Assert.That(materials.Bounds.X, Is.EqualTo(110));
            Assert.That(Ledges(panel), Has.Count.EqualTo(1), "one context, one ledge over all three");
        });
    }

    // Neither edge answer is acceptable - a ledge wider than its tabs lies about which it covers, a clipped title is
    // unreadable - so the run is WIDENED until the title fits, as Office does.
    [Test]
    public void ARunIsWidenedUntilItsTitleFits()
    {
        var group = Group("A VERY LONG CONTEXT TITLE");
        group.ActivatedAt = 1;

        var only = Header(40, group);
        var panel = Strip(Header(80), only);
        LayoutAt(panel, 600);

        var ledge = Ledges(panel)[0];

        Assert.Multiple(() =>
        {
            // The SLOT is what grew; a header with a stated Width reports that width whatever slot it was handed
            // (honest bounds), so the run is measured by where it starts and where the strip ends.
            Assert.That(panel.Bounds.Width - only.Bounds.X, Is.GreaterThan(40), "the run grew to hold its title");
            Assert.That(ledge.Bounds.Width, Is.EqualTo(panel.Bounds.Width - only.Bounds.X).Within(0.5),
                "and the ledge still spans exactly its run");
            Assert.That(ledge.Bounds.Width, Is.GreaterThanOrEqualTo(ledge.DesiredSize.Width),
                "which is what stops the title being clipped");
        });
    }

    // A run already wide enough is left alone - nothing is stretched for the sake of it.
    [Test]
    public void ARunWideEnoughIsLeftAsItIs()
    {
        var group = Group();
        group.ActivatedAt = 1;

        var wide = Header(300, group);
        var panel = Strip(wide);
        LayoutAt(panel, 600);

        Assert.That(wide.Bounds.Width, Is.EqualTo(300));
    }

    private static System.Collections.Generic.List<RibbonContextualLedge> Ledges(RibbonTabPanel panel)
    {
        var found = new System.Collections.Generic.List<RibbonContextualLedge>();
        foreach (var child in panel.VisualChildren)
        {
            if (child is RibbonContextualLedge ledge) found.Add(ledge);
        }

        return found;
    }
}
