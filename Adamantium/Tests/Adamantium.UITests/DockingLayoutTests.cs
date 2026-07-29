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
            Assert.That(split.Children.All(c => c.Length.IsStar), Is.True, "nobody was pinned to pixels by a split");
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
            Assert.That(split.Children[1].Length, Is.EqualTo(PaneLength.Stars(0.25)), "the newcomer takes the quarter it was given");
            Assert.That(split.Children.Sum(c => c.Length.Value), Is.EqualTo(1.0).Within(1e-9), "the pair is worth what the one of them was");
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
            Assert.That(split.Children.All(c => c.Length.IsStar), Is.True, "nobody was pinned to pixels by a split");
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
            Assert.That(layout.Main.Content.Length, Is.EqualTo(PaneLength.Star), "and it takes the whole space back");
        });
    }

    [Test]
    public void Normalize_FlattensNestedSplitsOfTheSameOrientation()
    {
        // Built by hand the way a careless caller might: a horizontal split holding another horizontal split.
        var outer = new PaneSplitNode { Orientation = Orientation.Horizontal };
        var inner = new PaneSplitNode { Orientation = Orientation.Horizontal, Length = PaneLength.Stars(0.5) };
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
            Assert.That(split.Children.All(c => c.Length.IsStar), Is.True, "nobody was pinned to pixels by a split");
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
            Assert.That(inner.Children[1].Length, Is.EqualTo(PaneLength.Pixels(220)), "the pixels the author wrote travel with the node");
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

    /// <summary>
    /// Dropping beside a group SPLITS THAT GROUP: the two of them share what the one of them had, and nobody else moves.
    /// The share is halved twice over - the target keeps half of its own, and the arrival takes the other half of its
    /// own - which is not the same as the arrival taking half of the whole row.
    /// <para>The editor case: a wide centre next to a narrow inspector. Drop a pane on the centre's right and the centre
    /// splits down the middle; the inspector is not involved and does not change width.</para>
    /// </summary>
    [Test]
    public void MovePane_BesideAGroupInAnAlreadySplitRow_HalvesThatGroupAndLeavesTheOthers()
    {
        var scene = Group("scene");
        var inspector = Group("inspector", "console");   // the console starts as one of the inspector's tabs

        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Right, inspector, 0.25);   // one horizontal row: centre 0.75 | inspector 0.25

        var sceneShare = scene.Length.Value;
        var inspectorShare = inspector.Length.Value;

        Assert.That(layout.MovePane("console", scene, DockZone.Right), Is.True);

        var row = scene.Parent;
        var arrived = (PaneGroupNode)row.Children[row.Children.IndexOf(scene) + 1];

        Assert.Multiple(() =>
        {
            Assert.That(arrived.PaneIds, Is.EqualTo(new[] { "console" }), "it landed to the RIGHT of the group it was dropped on");
            Assert.That(scene.Length.Value, Is.EqualTo(sceneShare / 2).Within(1e-6), "the group dropped on keeps half of its own share");
            Assert.That(arrived.Length.Value, Is.EqualTo(sceneShare / 2).Within(1e-6), "and the arrival takes the other half OF THAT GROUP");
            Assert.That(inspector.Length.Value, Is.EqualTo(inspectorShare).Within(1e-6), "an uninvolved neighbour is not touched at all");
            
        });
    }

    /// <summary>
    /// An EDGE ANCHOR - "along the whole left side of the area" - is not a second kind of docking. It is the same move
    /// aimed at the ROOT instead of at a group, which is why there is no second verb and no new zone for it: the root is
    /// a node like any other. The pane must end up spanning the full side, above every existing split.
    /// </summary>
    [Test]
    public void MovePane_AgainstTheRoot_SpansTheWholeSide()
    {
        var scene = Group("scene", "game");
        var inspector = Group("inspector");
        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Right, inspector, 0.25);   // [scene | inspector]

        Assert.That(layout.MovePane("game", layout.Main.Content, DockZone.Left), Is.True);

        var row = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(row.Orientation, Is.EqualTo(Orientation.Horizontal));
            Assert.That(row.Children, Has.Count.EqualTo(3), "the same row, one wider - not a split wrapping a split");
            Assert.That(((PaneGroupNode)row.Children[0]).PaneIds, Is.EqualTo(new[] { "game" }), "the newcomer holds the whole left side");
            Assert.That(((PaneGroupNode)row.Children[1]).PaneIds, Is.EqualTo(new[] { "scene" }));
            Assert.That(((PaneGroupNode)row.Children[2]).PaneIds, Is.EqualTo(new[] { "inspector" }));
        });
    }

    /// <summary>
    /// An edge anchor is a SIDE PANEL, not a half-split: the newcomer takes a band of the size asked for and everything
    /// else absorbs the rest. Half the area is what a drop BESIDE A GROUP means, and it is far too much for "dock this
    /// against the left edge" - an inspector is a couple of hundred pixels wide, not half the editor.
    /// </summary>
    [Test]
    public void MovePane_AgainstTheRoot_WithASize_TakesThatBandAndLeavesTheRestAlone()
    {
        var scene = Group("scene", "game");
        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Right, Group("inspector"), 0.25);

        Assert.That(layout.MovePane("game", layout.Main.Content, DockZone.Left, size: PaneLength.Pixels(240)), Is.True);

        var row = (PaneSplitNode)layout.Main.Content;
        var arrived = row.Children[0];

        Assert.Multiple(() =>
        {
            Assert.That(((PaneGroupNode)arrived).PaneIds, Is.EqualTo(new[] { "game" }));
            Assert.That(arrived.Length, Is.EqualTo(PaneLength.Pixels(240)), "the band it was given, in pixels");
            Assert.That(row.Children[1].Length.IsStar, Is.True, "and everything else takes what is left");
        });
    }

    /// <summary>The same drop ACROSS the root's own axis: the pane spans the full width above everything, and the whole
    /// existing layout becomes the other half. This is the case a group-relative drop cannot express at all.</summary>
    [Test]
    public void MovePane_AgainstTheRoot_AcrossItsAxis_PutsTheWholeLayoutBeside()
    {
        var scene = Group("scene", "game");
        var inspector = Group("inspector");
        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Right, inspector, 0.25);

        Assert.That(layout.MovePane("game", layout.Main.Content, DockZone.Top), Is.True);

        var outer = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(outer.Orientation, Is.EqualTo(Orientation.Vertical), "a top anchor stacks");
            Assert.That(((PaneGroupNode)outer.Children[0]).PaneIds, Is.EqualTo(new[] { "game" }), "the newcomer is the top band");
            Assert.That(outer.Children[1], Is.InstanceOf<PaneSplitNode>(), "and everything that was there is the rest");
        });
    }

    /// <summary>A root that is a single group has no "whole side" distinct from that group's side - and the answer is
    /// the same either way, which is what makes one verb enough.</summary>
    [Test]
    public void MovePane_AgainstARootThatIsOneGroup_SplitsIt()
    {
        var only = Group("scene", "game");
        var layout = LayoutWith(only);

        Assert.That(layout.MovePane("game", layout.Main.Content, DockZone.Left), Is.True);

        var row = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(((PaneGroupNode)row.Children[0]).PaneIds, Is.EqualTo(new[] { "game" }));
            Assert.That(((PaneGroupNode)row.Children[1]).PaneIds, Is.EqualTo(new[] { "scene" }));
        });
    }

    /// <summary>There is nothing to tab into but a group, so a centre drop on anything else is refused rather than
    /// invented.</summary>
    [Test]
    public void MovePane_IntoTheCentreOfASplit_IsRefused()
    {
        var scene = Group("scene", "game");
        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Right, Group("inspector"), 0.25);

        Assert.That(layout.MovePane("game", layout.Main.Content, DockZone.Center), Is.False);
    }

    /// <summary>The same halving when the drop makes a NEW split (the target's row runs the other way), so both routes
    /// through Split agree on what "beside" means.</summary>
    [Test]
    public void MovePane_BesideAGroupInANewSplit_HalvesThatGroup()
    {
        var scene = Group("scene");
        var console = Group("console");
        var layout = LayoutWith(scene);
        layout.Split(scene, DockZone.Bottom, console, 0.25);   // a vertical row; a Right drop must nest a new one

        Assert.That(layout.MovePane("console", scene, DockZone.Right), Is.True);

        var split = (PaneSplitNode)layout.Main.Content;
        Assert.Multiple(() =>
        {
            Assert.That(split.Orientation, Is.EqualTo(Orientation.Horizontal));
            Assert.That(split.Children[0].Length.Value, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(split.Children[1].Length.Value, Is.EqualTo(0.5).Within(1e-6));
        });
    }
}
