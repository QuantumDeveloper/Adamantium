using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What the AREA decides before anything leaves it: whether a gesture may happen at all, and what closing a pane means.
/// <para>The half of a gesture that opens a window is not here - a window needs an application context these tests do
/// not have - but every refusal is decided before that point, which is exactly what must not be got wrong: a refusal
/// discovered afterwards would mean undoing a move that has already happened.</para>
/// </summary>
[TestFixture]
public class DockingGestureTests
{
    private static PaneGroup Group(string name, DockZone zone, params string[] panes)
    {
        var group = new PaneGroup { Name = name, Zone = zone };
        foreach (var pane in panes) group.Items.Add(new Pane { Header = pane, Id = pane, Kind = PaneKind.Tool });
        return group;
    }

    private static DockingArea Area(params PaneGroup[] groups)
    {
        var area = new DockingArea { DividerThickness = 0 };
        foreach (var group in groups) area.Children.Add(group);

        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));
        return area;
    }

    private static Pane PaneOf(DockingArea area, string id) => area.PaneById(id);

    private static TabTearOffEventArgs TearArgs(Pane pane) => new(pane, pane, new Vector2(300, 300));

    /// <summary>The application says no, and the gesture ends as if it had never crossed its threshold - the pane stays
    /// where it was and no window is opened.</summary>
    [Test]
    public void ATearOffTheApplicationRefuses_LeavesThePaneWhereItIs()
    {
        var area = Area(Group("documents", DockZone.Center, "scene"), Group("tools", DockZone.Right, "inspector", "hierarchy"));

        var asked = 0;
        area.PaneTearingOff += (_, e) =>
        {
            asked++;
            e.Cancel = true;
        };

        var torn = area.TearOff(PaneOf(area, "inspector"), TearArgs(PaneOf(area, "inspector")));

        Assert.Multiple(() =>
        {
            Assert.That(torn, Is.False, "the gesture is refused");
            Assert.That(asked, Is.EqualTo(1), "and the application was asked exactly once");
            Assert.That(area.Layout.FindGroup("inspector").PaneIds, Does.Contain("inspector"), "the pane never left");
            Assert.That(area.Layout.Roots, Has.Count.EqualTo(1), "and no window's root was added");
        });
    }

    /// <summary>A pane that may not float does not tear off at all - the strip keeps it. Data (Pane.Allowed), not an
    /// event: where a pane may go is stated once and serialises with the layout.</summary>
    [Test]
    public void APaneThatMayNotFloat_DoesNotTearOff()
    {
        var area = Area(Group("documents", DockZone.Center, "scene"), Group("tools", DockZone.Right, "inspector"));

        var pinned = PaneOf(area, "inspector");
        pinned.Allowed = DockZone.Center | DockZone.Edges;   // everything except Floating

        Assert.Multiple(() =>
        {
            Assert.That(area.TearOff(pinned, TearArgs(pinned)), Is.False);
            Assert.That(area.Layout.Roots, Has.Count.EqualTo(1));
        });
    }

    /// <summary>One pane refusing to float holds the whole PANEL: dropping the group somewhere would take that pane
    /// there too, and a permission ignored when a pane travels in company is not a permission.</summary>
    [Test]
    public void OnePaneRefusingToFloat_HoldsTheWholePanel()
    {
        var tools = Group("tools", DockZone.Right, "inspector", "hierarchy");
        var area = Area(Group("documents", DockZone.Center, "scene"), tools);

        PaneOf(area, "hierarchy").Allowed = DockZone.Center | DockZone.Edges;

        Assert.That(area.TearOffGroup(tools, new Vector2(300, 300)), Is.False,
            "the panel stays, because one of its panes may not leave with it");
    }

    /// <summary>Closing a DOCUMENT is final, and it is announced: anything keeping its own account of what is open has
    /// to hear it, or it goes on believing a closed pane is still there.</summary>
    [Test]
    public void ClosingADocument_IsFinalAndAnnounced()
    {
        var area = Area(Group("documents", DockZone.Center, "scene", "game"));
        var doc = PaneOf(area, "game");
        doc.Kind = PaneKind.Document;

        string closed = null;
        var restorable = true;
        area.PaneClosed += (_, e) =>
        {
            closed = e.PaneId;
            restorable = e.CanRestore;
        };

        area.ClosePane(doc);

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.EqualTo("game"), "the closing was announced");
            Assert.That(restorable, Is.False, "a document is gone for good");
            Assert.That(area.Layout.FindGroup("game"), Is.Null, "and it is out of the layout");
            Assert.That(area.HiddenPanes, Does.Not.Contain("game"), "nothing keeps it for a menu");
        });
    }

    /// <summary>Closing a TOOL puts it away instead: it is part of the workspace, so it stays reachable from a menu and
    /// comes back where it stood.</summary>
    [Test]
    public void ClosingATool_PutsItAwayAndItComesBack()
    {
        var area = Area(Group("documents", DockZone.Center, "scene"), Group("tools", DockZone.Right, "inspector", "hierarchy"));
        var tool = PaneOf(area, "hierarchy");

        var restorable = false;
        area.PaneClosed += (_, e) => restorable = e.CanRestore;

        area.ClosePane(tool);

        Assert.Multiple(() =>
        {
            Assert.That(restorable, Is.True, "a tool can be brought back");
            Assert.That(area.HiddenPanes, Does.Contain("hierarchy"), "and is listed for the Windows menu");
            Assert.That(area.Layout.FindGroup("hierarchy"), Is.Null, "while it is away it is not in the layout");
        });

        Assert.That(area.RestorePane("hierarchy"), Is.True);

        Assert.Multiple(() =>
        {
            var home = area.Layout.FindGroup("hierarchy");
            Assert.That(home, Is.Not.Null, "it is back in the layout");
            Assert.That(home.PaneIds, Does.Contain("inspector"), "in the very panel it was closed from");
            Assert.That(area.HiddenPanes, Does.Not.Contain("hierarchy"), "and off the menu again");
        });
    }
}
