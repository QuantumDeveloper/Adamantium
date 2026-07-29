using System.Linq;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The docking layout is plain data, so it is tested as plain data - no window, no GPU, no dragging. That is the whole
/// point of keeping the model out of the controls: a layout that can only be produced by mouse cannot be asserted on.
/// </summary>
[TestFixture]
public class DockingLayoutTests
{
    private static PaneGroupNode Group(params string[] panes)
    {
        var group = new PaneGroupNode();
        foreach (var pane in panes) group.Add(pane);
        return group;
    }

    private static DockingLayout LayoutWith(PaneNode content)
    {
        var layout = new DockingLayout();
        layout.Roots.Add(new DockingRoot(content, isMain: true));
        return layout;
    }

    /// <summary>The centre drop: the pane simply joins the target's tabs. This is the common case and it must not grow
    /// a level in the tree for nothing.</summary>
    [Test]
    public void MovePane_IntoTheCentre_JoinsTheTargetsTabs()
    {
        var documents = Group("scene", "game");
        var tools = Group("inspector");
        var layout = LayoutWith(documents);
        layout.Split(documents, DockZone.Right, tools);

        Assert.That(layout.MovePane("inspector", documents, DockZone.Center), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Main.Content, Is.SameAs(documents), "the emptied split collapsed away");
            Assert.That(documents.PaneIds, Is.EqualTo(new[] { "scene", "game", "inspector" }));
            Assert.That(layout.FindGroup("inspector"), Is.SameAs(documents));
        });
    }

    [Test]
    public void MovePane_ToASide_SplitsAndLeavesNothingBehind()
    {
        var documents = Group("scene", "game");
        var layout = LayoutWith(documents);

        Assert.That(layout.MovePane("game", documents, DockZone.Bottom), Is.True);

        var split = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(split.Orientation, Is.EqualTo(Orientation.Vertical));
            Assert.That(((PaneGroupNode)split.Children[0]).PaneIds, Is.EqualTo(new[] { "scene" }), "the pane left its old group");
            Assert.That(((PaneGroupNode)split.Children[1]).PaneIds, Is.EqualTo(new[] { "game" }));
            Assert.That(split.Children.Sum(c => c.Fraction), Is.EqualTo(1.0).Within(1e-9));
        });
    }

    /// <summary>Dropping a group's ONLY pane beside itself asks for nothing, and must not be attempted: the removal
    /// empties the target, and the split would then be made against a node the tree no longer holds.</summary>
    [Test]
    public void MovePane_ASoleTabOntoItsOwnSide_IsRefused()
    {
        var only = Group("scene");
        var layout = LayoutWith(only);

        Assert.Multiple(() =>
        {
            Assert.That(layout.MovePane("scene", only, DockZone.Right), Is.False);
            Assert.That(layout.Main.Content, Is.SameAs(only), "the tree is untouched");
            Assert.That(only.PaneIds, Is.EqualTo(new[] { "scene" }));
        });
    }

    [Test]
    public void MovePane_EmptyingAGroup_RemovesItAndTheLevelItLeaves()
    {
        var documents = Group("scene");
        var tools = Group("inspector");
        var layout = LayoutWith(documents);
        layout.Split(documents, DockZone.Right, tools);

        layout.MovePane("inspector", documents, DockZone.Center);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FindGroup("inspector"), Is.SameAs(documents));
            Assert.That(layout.Main.Content, Is.InstanceOf<PaneGroupNode>(), "one group left means no split left");
        });
    }

    [Test]
    public void Split_PutsTheNewcomerOnTheGivenSide()
    {
        var scene = Group("scene");
        var layout = LayoutWith(scene);

        layout.Split(scene, DockZone.Right, Group("inspector"), 0.25);

        var split = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(split.Orientation, Is.EqualTo(Orientation.Horizontal), "left/right splits run horizontally");
            Assert.That(((PaneGroupNode)split.Children[0]).PaneIds, Is.EqualTo(new[] { "scene" }), "the target keeps its side");
            Assert.That(((PaneGroupNode)split.Children[1]).PaneIds, Is.EqualTo(new[] { "inspector" }), "the newcomer is on the right");
            Assert.That(split.Children[1].Fraction, Is.EqualTo(0.25).Within(1e-9));
            Assert.That(split.Children.Sum(c => c.Fraction), Is.EqualTo(1.0).Within(1e-9), "shares always add up to one");
        });
    }

    [Test]
    public void Split_OnTheSameAxisTwice_DoesNotNest()
    {
        var scene = Group("scene");
        var layout = LayoutWith(scene);

        layout.Split(scene, DockZone.Right, Group("inspector"), 0.25);
        layout.Split(scene, DockZone.Right, Group("console"), 0.25);

        var split = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(split.Children, Has.Count.EqualTo(3), "one split of three, not a split inside a split");
            Assert.That(split.Children.All(c => c is PaneGroupNode), Is.True, "every child is a leaf");
            Assert.That(split.Children.Sum(c => c.Fraction), Is.EqualTo(1.0).Within(1e-9));
        });
    }

    [Test]
    public void ClosingTheLastPane_CollapsesTheLevelItLeaves()
    {
        var scene = Group("scene");
        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Right, Group("inspector"), 0.25);

        layout.RemovePane("inspector");

        Assert.Multiple(() =>
        {
            Assert.That(layout.Main.Content, Is.InstanceOf<PaneGroupNode>(), "a split dividing one child is not a split");
            Assert.That(((PaneGroupNode)layout.Main.Content).PaneIds, Is.EqualTo(new[] { "scene" }));
            Assert.That(layout.Main.Content.Fraction, Is.EqualTo(1.0).Within(1e-9), "and it takes the whole space back");
        });
    }

    [Test]
    public void Normalize_FlattensNestedSplitsOfTheSameOrientation()
    {
        // Built by hand the way a careless caller might: a horizontal split holding another horizontal split.
        var outer = new PaneSplitNode { Orientation = Orientation.Horizontal };
        var inner = new PaneSplitNode { Orientation = Orientation.Horizontal, Fraction = 0.5 };
        inner.Add(Group("a"));
        inner.Add(Group("b"));
        outer.Add(Group("c"));
        outer.Add(inner);

        var layout = LayoutWith(outer);
        layout.Normalize();

        var split = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(split.Children, Has.Count.EqualTo(3), "three siblings, not two with one of them a split");
            Assert.That(split.Children.All(c => c is PaneGroupNode), Is.True);
            Assert.That(split.Children.Sum(c => c.Fraction), Is.EqualTo(1.0).Within(1e-9));
        });
    }

    [Test]
    public void Normalize_DropsAnEmptiedFloatingRoot_ButKeepsTheMainOne()
    {
        var layout = LayoutWith(Group("scene"));
        var floating = new DockingRoot(Group("inspector"));
        layout.Roots.Add(floating);

        layout.RemovePane("inspector");
        layout.RemovePane("scene");

        Assert.Multiple(() =>
        {
            Assert.That(layout.Roots, Has.Count.EqualTo(1), "the floating root goes when its last pane does");
            Assert.That(layout.Roots[0].IsMain, Is.True, "the main window stays - an app without one is not a layout");
            Assert.That(layout.Main.Content, Is.Null, "empty, but still there");
        });
    }

    [Test]
    public void RemovingTheActivePane_LeavesTheGroupPointingAtARealOne()
    {
        var group = Group("a", "b", "c");
        group.ActiveIndex = 2;
        var layout = LayoutWith(group);

        layout.RemovePane("c");

        Assert.Multiple(() =>
        {
            Assert.That(group.PaneIds, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(group.ActiveIndex, Is.EqualTo(1), "the last pane's neighbour becomes active, not a hole");
        });
    }

    /// <summary>Markup names ZONES, never shares - and the tree comes out of them.</summary>
    [Test]
    public void FromZones_BuildsTheTreeFromWhereThingsWereDeclared()
    {
        var layout = DockingLayout.FromZones(new[]
        {
            new ZoneDeclaration(DockZone.Center, Group("scene")),
            new ZoneDeclaration(DockZone.Right, Group("inspector"), 220),
            new ZoneDeclaration(DockZone.Bottom, Group("console"), 180)
        });

        // Last declared is outermost: the bottom strip spans under everything above it.
        var outer = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(outer.Orientation, Is.EqualTo(Orientation.Vertical), "bottom docks along the vertical axis");
            Assert.That(outer.Children, Has.Count.EqualTo(2));
            Assert.That(((PaneGroupNode)outer.Children[1]).PaneIds, Is.EqualTo(new[] { "console" }));

            var inner = (PaneSplitNode)outer.Children[0];
            Assert.That(inner.Orientation, Is.EqualTo(Orientation.Horizontal), "right docks along the horizontal axis");
            Assert.That(((PaneGroupNode)inner.Children[0]).PaneIds, Is.EqualTo(new[] { "scene" }));
            Assert.That(((PaneGroupNode)inner.Children[1]).PaneIds, Is.EqualTo(new[] { "inspector" }));
            Assert.That(inner.Children[1].DesiredSize, Is.EqualTo(220), "the pixel hint travels with the node");
        });
    }

    /// <summary>A second Center group joins the documents rather than splitting anything.</summary>
    [Test]
    public void FromZones_CenterTwice_LandsInTheSameGroup()
    {
        var layout = DockingLayout.FromZones(new[]
        {
            new ZoneDeclaration(DockZone.Center, Group("scene")),
            new ZoneDeclaration(DockZone.Center, Group("game"))
        });

        Assert.That(layout.Main.Content, Is.InstanceOf<PaneGroupNode>(), "no split - they share one area");
        Assert.That(((PaneGroupNode)layout.Main.Content).PaneIds, Is.EqualTo(new[] { "scene", "game" }));
    }

    [Test]
    public void FindGroup_LooksThroughEveryRoot()
    {
        var layout = LayoutWith(Group("scene"));
        layout.Roots.Add(new DockingRoot(Group("inspector")));

        Assert.Multiple(() =>
        {
            Assert.That(layout.FindGroup("inspector"), Is.Not.Null, "a floating root is searched like any other");
            Assert.That(layout.FindGroup("nobody"), Is.Null);
        });
    }
}
