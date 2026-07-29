using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Docking a pane BACK into a group must leave its tab strip laid out - the tabs side by side, each at its own place.
/// The bug this pins down: after a dock-back the newly arrived tab sat on top of a neighbour, because it reached the
/// arrange pass never having been measured (a tab of zero desired extent starts where the previous one started).
/// <para>Driven through the REAL layout manager (<see cref="WindowExtension.UpdateTree"/>), not by calling Measure and
/// Arrange by hand: calling them directly lays out everything unconditionally and so cannot see a lost invalidation,
/// which is the whole question here.</para>
/// </summary>
[TestFixture]
public class DockRedockLayoutTests
{
    private const double TabWidth = 80;
    private const double TabHeight = 24;

    /// <summary>A visual root with a client viewport, mirroring a window's role (same shape the layout-manager tests use).</summary>
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

    // The strip the theme gives a group, reduced to what decides tab positions: an items presenter hosting a TabPanel.
    private static PaneGroup Group(DockZone zone, params string[] panes)
    {
        var group = new PaneGroup
        {
            Zone = zone,
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new TabPanel { Orientation = Orientation.Horizontal }
            }),
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        foreach (var name in panes)
            group.Items.Add(new Pane { Header = name, Id = name, Width = TabWidth, Height = TabHeight });

        return group;
    }

    private static TestWindowRoot Rooted(DockingArea area)
    {
        var root = new TestWindowRoot { Width = 1000, Height = 700, ClientWidth = 1000, ClientHeight = 700 };
        root.Children.Add(area);
        return root;
    }

    // Where each tab of a group actually ended up along the strip.
    private static double[] TabPositions(PaneGroup group)
    {
        return Enumerable.Range(0, group.Items.Count)
            .Select(i => (TabItem)group.ItemContainerGenerator.ContainerFromIndex(i))
            .Select(tab => tab.Bounds.X)
            .ToArray();
    }

    /// <summary>The baseline: a group that was never touched lays its tabs out end to end. If this fails nothing below
    /// means anything.</summary>
    [Test]
    public void TabsOfAnUntouchedGroup_SitEndToEnd()
    {
        var area = new DockingArea { DividerThickness = 0 };
        area.Children.Add(Group(DockZone.Center, "scene", "game"));
        var root = Rooted(area);

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        Assert.That(TabPositions(GroupControl(area, "scene")), Is.EqualTo(new[] { 0.0, TabWidth }).Within(0.5));
    }

    /// <summary>The dock-back itself: a pane living in its own group joins another group's tabs - the very move the
    /// compass's centre drop performs. All three tabs must then have their own place on the strip.</summary>
    [Test]
    public void AfterAPaneIsDockedBack_TheTabsDoNotOverlap()
    {
        var area = new DockingArea { DividerThickness = 0 };
        area.Children.Add(Group(DockZone.Center, "scene", "game"));
        area.Children.Add(Group(DockZone.Right, "inspector"));
        var root = Rooted(area);

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        var documents = area.Layout.FindGroup("scene");
        Assert.That(area.Layout.MovePane("inspector", documents, DockZone.Center), Is.True, "the model accepted the move");
        area.Rebuild();

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        var group = GroupControl(area, "scene");
        var positions = TabPositions(group);

        Assert.Multiple(() =>
        {
            Assert.That(group.Items.Count, Is.EqualTo(3), "the pane arrived as a third tab");
            Assert.That(positions, Is.EqualTo(new[] { 0.0, TabWidth, TabWidth * 2 }).Within(0.5),
                "every tab has its own place on the strip - an overlap means one was arranged without ever being measured");
        });
    }

    /// <summary>Same move, asked the other way round: whatever the numbers are, no two tabs may start at the same place.
    /// Stated separately so a change in tab metrics can never make the overlap check vacuous.</summary>
    [Test]
    public void AfterAPaneIsDockedBack_NoTwoTabsShareAPosition()
    {
        var area = new DockingArea { DividerThickness = 0 };
        area.Children.Add(Group(DockZone.Center, "scene", "game"));
        area.Children.Add(Group(DockZone.Bottom, "console"));
        var root = Rooted(area);

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        area.Layout.MovePane("console", area.Layout.FindGroup("scene"), DockZone.Center);
        area.Rebuild();

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        var positions = TabPositions(GroupControl(area, "scene"));
        Assert.That(positions.Distinct().Count(), Is.EqualTo(positions.Length),
            $"two tabs at one position: [{string.Join(", ", positions)}]");
    }

    /// <summary>
    /// The style cycle a control goes through whenever it is RE-PARENTED: SetParent calls ApplyCurrentTheme, which
    /// detaches the previously applied styles before applying the theme again. Detaching removes the ItemsPanel setter's
    /// value, so the property really does go to null and back - which is not a no-op re-application and so is not caught
    /// by the "same instance" guard. Standing in for it here by writing null and the template back, because the real
    /// call needs an application context; the writes are exactly the ones Style.Detach + Style.Attach make.
    /// </summary>
    [Test]
    public void AStyleCycle_LeavesTheTabsInThePanelThatIsLaidOut()
    {
        var area = new DockingArea { DividerThickness = 0 };
        area.Children.Add(Group(DockZone.Center, "scene", "game", "console"));
        var root = Rooted(area);

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        var group = GroupControl(area, "scene");
        var template = group.ItemsPanel;
        var before = group.ItemsHostPanel;

        group.ItemsPanel = null;        // Style.Detach: the setter's value is removed
        group.ItemsPanel = template;    // Style.Attach: the theme puts it back

        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        Assert.Multiple(() =>
        {
            Assert.That(before?.VisualParent, Is.Null,
                "the panel that was replaced must be detached - an orphan still in the tree goes on being laid out");
            Assert.That(TabPositions(group), Is.EqualTo(new[] { 0.0, TabWidth, TabWidth * 2 }).Within(0.5),
                "the tabs are laid out in the panel that is actually arranged");
        });
    }

    // The group control showing the node that holds a given pane.
    private static PaneGroup GroupControl(DockingArea area, string paneId)
    {
        var pane = Descendants(area).OfType<Pane>().First(p => p.Id == paneId);
        for (var parent = pane.VisualParent; parent != null; parent = parent.VisualParent)
        {
            if (parent is PaneGroup group) return group;
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<IUIComponent> Descendants(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
