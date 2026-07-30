using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The area turns AUTHORED ZONES into rectangles. Checked as rectangles - no window, no GPU: the author says "inspector
/// on the right, 200 wide" and the only question worth asking is where things actually land.
/// </summary>
[TestFixture]
public class DockingAreaTests
{
    /// <summary>A group with a single pane in it. The pane matters: the model names PANES, so a group holding none has
    /// nothing to place and is dropped - which is right, and is why every fixture here puts one in.</summary>
    private static PaneGroup Group(string name, DockZone zone, double size = double.NaN)
    {
        var group = new PaneGroup { Name = name, Zone = zone, Size = size };
        group.Items.Add(new Pane { Header = name, Id = name });
        return group;
    }

    [Test]
    public void ZonesBecomeRectangles_SidesFullHeight_BandInsideTheCentreColumn()
    {
        var area = new DockingArea { DividerThickness = 0 };
        var centre = Group("scene", DockZone.Center);
        var right = Group("inspector", DockZone.Right);
        var bottom = Group("console", DockZone.Bottom);
        area.Children.Add(centre);
        area.Children.Add(right);
        area.Children.Add(bottom);

        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));

        Assert.Multiple(() =>
        {
            // The SIDE takes the full height of the area: it is the outermost split, whatever order the zones were
            // declared in. A tool docked at an edge belongs to the window, not to the space above the bottom band.
            Assert.That(right.Bounds.Height, Is.EqualTo(800).Within(0.5), "the side runs the whole height");
            Assert.That(right.Bounds.Y, Is.EqualTo(0).Within(0.5), "from the very top");
            Assert.That(right.Bounds.X + right.Bounds.Width, Is.EqualTo(1000).Within(0.5), "and ends at the edge");

            // The band lives INSIDE the centre column: under the documents, stopping where the side begins.
            Assert.That(bottom.Bounds.X, Is.EqualTo(0).Within(0.5));
            Assert.That(bottom.Bounds.Width, Is.EqualTo(right.Bounds.X).Within(0.5), "it stops at the side, not under it");
            Assert.That(bottom.Bounds.Y + bottom.Bounds.Height, Is.EqualTo(800).Within(0.5), "and reaches the bottom edge");

            Assert.That(centre.Bounds.X, Is.EqualTo(0).Within(0.5));
            Assert.That(centre.Bounds.Width, Is.EqualTo(bottom.Bounds.Width).Within(0.5), "documents and band share one column");
        });
    }

    [Test]
    public void DividersTakeTheirSpaceOutOfTheSharedExtent()
    {
        var area = new DockingArea { DividerThickness = 6 };
        area.Children.Add(Group("scene", DockZone.Center));
        var right = Group("inspector", DockZone.Right);
        area.Children.Add(right);

        area.Measure(new Size(506, 400));
        area.Arrange(new Rect(0, 0, 506, 400));

        Assert.That(right.Bounds.X + right.Bounds.Width, Is.EqualTo(506).Within(0.5),
            "the far edge is still reached exactly - the divider comes out of the middle, not off the end");
    }

    /// <summary>
    /// An authored pixel size is stated along the ZONE's axis - "the console starts about 160 tall". Drop something
    /// beside it and that group is now in a row running the other way, where the number says nothing: the two must
    /// simply share the row. Spending it anyway squeezed the console to 160 WIDE and gave the newcomer everything else.
    /// </summary>
    [Test]
    public void APixelSizeStatedForOneAxis_IsNotSpentOnTheOther()
    {
        var area = new DockingArea { DividerThickness = 0 };
        var centre = Group("scene", DockZone.Center);
        centre.Items.Add(new Pane { Header = "game", Id = "game" });
        var console = Group("console", DockZone.Bottom, size: 160);
        area.Children.Add(centre);
        area.Children.Add(console);

        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));

        // "game" joins the console's row, to its right - the bottom row now runs HORIZONTALLY.
        Assert.That(area.Layout.MovePane("game", area.Layout.FindGroup("console"), DockZone.Right), Is.True);
        area.Rebuild();

        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));

        // Asserted on what is on SCREEN, not on the model's fractions: the hint is spent by the host during layout, so
        // the model can be perfectly halved while the arranged widths are 160 and the rest.
        var consoleWidth = GroupControl(area, "console").Bounds.Width;
        var gameWidth = GroupControl(area, "game").Bounds.Width;

        Assert.Multiple(() =>
        {
            Assert.That(consoleWidth, Is.EqualTo(500).Within(1), "they share the row they now both live in");
            Assert.That(gameWidth, Is.EqualTo(500).Within(1));

            // And the number the author DID state still holds: the area is 160 tall, now shared by two panes instead of
            // one. Only the axis that was split may change.
            Assert.That(GroupControl(area, "console").Bounds.Height, Is.EqualTo(160).Within(1),
                "the height the author asked for survives a split across the other axis");
            Assert.That(GroupControl(area, "game").Bounds.Height, Is.EqualTo(160).Within(1));
        });
    }

    // The group control holding a given pane, found in the visual tree the area built. By the group's ITEMS rather than
    // by walking up from the pane: with no theme here a group has no template, so its panes are never realized into the
    // visual tree - but the group itself is.
    private static PaneGroup GroupControl(DockingArea area, string paneId)
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
    /// A REVEALED panel is a glance at a tool: a press anywhere outside it puts it away again, and only pinning keeps it.
    /// <para>Dismissal itself now belongs to the FLYOUT (a Popup with KeepOpen=false), because the revealed body lives in
    /// the window's popup layer - outside this group's own subtree - so the area's "was the press inside the group?" test
    /// would count a press on the panel's own content as a press elsewhere. What is asserted here is the half that is
    /// still the area's: that being dismissed puts the panel away rather than leaving it half-shown.</para>
    /// </summary>
    [Test]
    public void DismissingARevealedPanel_PutsItAway()
    {
        var area = new DockingArea { DividerThickness = 0 };
        var centre = Group("scene", DockZone.Center);
        var right = Group("inspector", DockZone.Right, 240);
        area.Children.Add(centre);
        area.Children.Add(right);

        var root = new TestWindowRoot { Width = 1000, Height = 800, ClientWidth = 1000, ClientHeight = 800 };
        root.Children.Add(area);
        root.Measure(new Size(1000, 800));
        root.Arrange(new Rect(0, 0, 1000, 800));

        var node = area.Layout.FindGroup("inspector");
        Assert.That(area.Layout.CollapseGroup(node), Is.True);
        area.Rebuild();
        Assert.That(area.Layout.RevealGroup(node), Is.True);
        area.Rebuild();
        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));

        Assert.That(node.State, Is.EqualTo(PaneGroupState.Revealed), "showing, but not pinned");

        // What the flyout's light dismiss calls when it closes itself.
        area.Hide(right);

        Assert.Multiple(() =>
        {
            Assert.That(node.State, Is.EqualTo(PaneGroupState.Collapsed), "the glance is over");
            Assert.That(node.RestoreLength, Is.EqualTo(PaneLength.Pixels(240)), "and what it is worth docked survives");
        });
    }

    private sealed class TestWindowRoot : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(Vector2 point) => point;
        public Vector2 PointToScreen(Vector2 point) => point;
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    [Test]
    public void ASingleGroup_TakesEverything()
    {
        var area = new DockingArea();
        var only = Group("scene", DockZone.Center);
        area.Children.Add(only);

        area.Measure(new Size(640, 480));
        area.Arrange(new Rect(0, 0, 640, 480));

        Assert.Multiple(() =>
        {
            Assert.That(only.Bounds.Width, Is.EqualTo(640).Within(0.5));
            Assert.That(only.Bounds.Height, Is.EqualTo(480).Within(0.5));
        });
    }
}
