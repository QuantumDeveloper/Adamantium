using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What the layout is allowed to do to the sizes it hands out: the document well has a floor, minimums are honoured
/// wherever a size comes from, and the area does not hold on to controls for nodes that have died.
/// </summary>
[TestFixture]
public class DockingSizingTests
{
    private static PaneGroup Group(string name, DockZone zone, double size = double.NaN)
    {
        var group = new PaneGroup { Name = name, Zone = zone, Size = size };
        group.Items.Add(new Pane { Header = name, Id = name });
        return group;
    }

    private static DockingArea Area(params PaneGroup[] groups)
    {
        var area = new DockingArea { DividerThickness = 0 };
        foreach (var group in groups) area.Children.Add(group);
        return area;
    }

    private static void Lay(DockingArea area, double width, double height)
    {
        area.Measure(new Size(width, height));
        area.Arrange(new Rect(0, 0, width, height));
    }

    private static PaneGroup Control(DockingArea area, string paneId)
    {
        return Descendants(area).OfType<PaneGroup>()
            .FirstOrDefault(g => g.Items.OfType<Pane>().Any(p => p.Id == paneId));
    }

    private static System.Collections.Generic.IEnumerable<IUIComponent> Descendants(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    /// <summary>
    /// Rule 7.6. Every tool docked against the centre is paid for by the centre, so without a floor enough of them
    /// squeeze it out of existence - measured before the fix at 60px against a stated minimum of 200.
    /// </summary>
    [Test]
    public void TheDocumentWellIsNeverSqueezedBelowItsFloor()
    {
        var area = Area(Group("scene", DockZone.Center));
        area.DocumentMinSize = 200;

        // Four side panels, each asking for a band of its own out of a 700px-wide area: 4 x 240 is more than there is.
        for (var i = 0; i < 4; i++)
        {
            area.AddPane(new Pane { Header = $"tool{i}", Id = $"tool{i}", Kind = PaneKind.Tool }, DockZone.Left);
        }

        Lay(area, 700, 600);

        Assert.That(Control(area, "scene").Bounds.Width, Is.GreaterThanOrEqualTo(200 - 0.5),
            "the centre keeps its floor and the bands are squeezed instead");
    }

    /// <summary>A share is not a permission to disappear: whoever falls under its minimum is pinned at it, and the cost
    /// comes out of those that still have room. Before this, minimums were a splitter's business alone.</summary>
    [Test]
    public void MinimumsAreHonouredWhereverTheSizeComesFrom()
    {
        var area = Area(Group("scene", DockZone.Center), Group("inspector", DockZone.Right, size: 240));
        area.DocumentMinSize = 200;

        // Narrower than the two of them want together: 240 + 200 does not fit in 300.
        Lay(area, 300, 600);

        Assert.Multiple(() =>
        {
            var centre = Control(area, "scene").Bounds.Width;
            var side = Control(area, "inspector").Bounds.Width;

            Assert.That(centre + side, Is.EqualTo(300).Within(1), "between them they still fill the area exactly");
            Assert.That(centre, Is.GreaterThan(0), "and neither is squeezed out of existence");
            Assert.That(side, Is.GreaterThan(0));
        });
    }

    /// <summary>A pane opened from CODE joins the panel already on that side. Opening from code cannot see what a new
    /// column would cost, and each one used to take its band off the centre until the layout was a row of slivers.</summary>
    [Test]
    public void APaneOpenedOnAnOccupiedSideBecomesATabThere()
    {
        var area = Area(Group("scene", DockZone.Center), Group("inspector", DockZone.Right, size: 240));
        Lay(area, 1000, 800);

        area.AddPane(new Pane { Header = "hierarchy", Id = "hierarchy", Kind = PaneKind.Tool }, DockZone.Right);
        Lay(area, 1000, 800);

        var side = area.Layout.FindGroup("inspector");

        Assert.Multiple(() =>
        {
            Assert.That(side.PaneIds, Does.Contain("hierarchy"), "it joined the panel that was already there");
            Assert.That(Control(area, "hierarchy"), Is.SameAs(Control(area, "inspector")), "one control, two tabs");
        });
    }

    /// <summary>The area keeps a control per model node ACROSS rebuilds, so it has to let go of the nodes that die -
    /// groups when their last pane leaves, and split hosts when a row is left with one child and collapses away.</summary>
    [Test]
    public void ControlsOfDeadNodesAreForgotten()
    {
        var area = Area(Group("scene", DockZone.Center));
        Lay(area, 1000, 800);

        var groupsAtRest = area.TrackedGroups;
        var hostsAtRest = area.TrackedHosts;

        // Open a panel on each side and close it again, several times over. Every round builds a split and then
        // collapses it, so anything not pruned accumulates.
        for (var i = 0; i < 5; i++)
        {
            var pane = new Pane { Header = "tool", Id = "tool", Kind = PaneKind.Document };
            area.AddPane(pane, DockZone.Left);
            Lay(area, 1000, 800);

            area.RemovePane("tool");
            Lay(area, 1000, 800);
        }

        Assert.Multiple(() =>
        {
            Assert.That(area.TrackedGroups, Is.EqualTo(groupsAtRest), "no group control is held for a node that died");
            Assert.That(area.TrackedHosts, Is.EqualTo(hostsAtRest), "and no split host either");
        });
    }
}
