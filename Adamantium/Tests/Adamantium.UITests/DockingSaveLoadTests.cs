using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A saved layout is the arrangement written down: the tree, the edge bars, which panel is put away, which tab is on
/// top and where a floating window sits. Asserted on the MODEL, which is the whole point of the model being data.
/// </summary>
[TestFixture]
public class DockingSaveLoadTests
{
    private static DockingLayout Editor()
    {
        var documents = new PaneGroupNode();
        documents.Add("scene");
        documents.Add("game");
        documents.ActiveIndex = 1;

        var inspector = new PaneGroupNode();
        inspector.Add("inspector");
        inspector.Add("hierarchy");

        var layout = DockingLayout.FromZones(
        [
            new ZoneDeclaration(DockZone.Center, documents, double.NaN),
            new ZoneDeclaration(DockZone.Right, inspector, 240)
        ]);

        return layout;
    }

    private static DockingLayout RoundTrip(DockingLayout layout, System.Func<string, bool> keep = null)
    {
        var text = DockingLayoutSerializer.Save(layout, keep);
        Assert.That(text, Is.Not.Null.And.Not.Empty);
        return DockingLayoutSerializer.Load(text);
    }

    [Test]
    public void TheTreeComesBackWithItsSizesAndItsActiveTabs()
    {
        var saved = Editor();
        var back = RoundTrip(saved);

        var documents = back.FindGroup("scene");
        var inspector = back.FindGroup("inspector");

        Assert.Multiple(() =>
        {
            Assert.That(documents, Is.Not.Null);
            Assert.That(documents.PaneIds, Is.EqualTo(new[] { "scene", "game" }), "in order");
            Assert.That(documents.ActiveIndex, Is.EqualTo(1), "and looking at the tab it was looking at");

            Assert.That(inspector.PaneIds, Is.EqualTo(new[] { "inspector", "hierarchy" }));
            Assert.That(inspector.Length, Is.EqualTo(PaneLength.Pixels(240)), "the band keeps the width it was given");

            Assert.That(back.DocumentWell, Is.SameAs(documents), "and the centre is still a PLACE, not just a group");
        });
    }

    [Test]
    public void APutAwayPanelComesBackPutAway_OnItsOwnEdge()
    {
        var saved = Editor();
        var inspector = saved.FindGroup("inspector");
        Assert.That(saved.CollapseGroup(inspector), Is.True);

        var back = RoundTrip(saved);
        var restored = back.FindGroup("inspector");
        var root = back.Main;

        Assert.Multiple(() =>
        {
            Assert.That(restored.State, Is.EqualTo(PaneGroupState.Collapsed));
            Assert.That(root.EdgeOfBarred(restored), Is.EqualTo(DockZone.Right), "on the edge it was folded against");
            Assert.That(restored.Parent, Is.Null, "and out of the tree, where a put-away panel belongs (rule 3b)");
            Assert.That(restored.RestoreLength, Is.EqualTo(PaneLength.Pixels(240)), "still worth what it was docked at");
        });
    }

    /// <summary>A REVEALED panel is a glance at a tool, not an arrangement: reopening with a flyout hanging over the
    /// layout would restore a gesture.</summary>
    [Test]
    public void ARevealedPanelComesBackMerelyPutAway()
    {
        var saved = Editor();
        var inspector = saved.FindGroup("inspector");
        saved.CollapseGroup(inspector);
        Assert.That(saved.RevealGroup(inspector), Is.True);

        var back = RoundTrip(saved);

        Assert.That(back.FindGroup("inspector").State, Is.EqualTo(PaneGroupState.Collapsed));
    }

    /// <summary>Where a floating window sits is the ONE absolute number a layout keeps - it is why a panel left on a
    /// second monitor comes back to that monitor.</summary>
    [Test]
    public void AFloatingWindowKeepsItsPlaceOnScreen()
    {
        var saved = Editor();
        var torn = saved.TearOffGroup(saved.FindGroup("inspector"));
        torn.Bounds = new Rect(1920, 240, 500, 700);

        var back = RoundTrip(saved);
        var floating = back.Roots.FirstOrDefault(r => !r.IsMain);

        Assert.Multiple(() =>
        {
            Assert.That(floating, Is.Not.Null, "the window is a root of the layout, so it is saved with it");
            Assert.That(floating.Bounds.X, Is.EqualTo(1920));
            Assert.That(floating.Bounds.Y, Is.EqualTo(240));
            Assert.That(floating.Bounds.Width, Is.EqualTo(500));
            Assert.That(floating.Bounds.Height, Is.EqualTo(700));
        });
    }

    /// <summary>Documents belong to a SESSION: the file may not even exist next time, so they are not written down -
    /// and the group they leave behind is written without them.</summary>
    [Test]
    public void PanesThatDoNotComeBack_AreNotSaved()
    {
        var saved = Editor();

        var back = RoundTrip(saved, keep: id => id != "game");
        var documents = back.FindGroup("scene");

        Assert.Multiple(() =>
        {
            Assert.That(documents.PaneIds, Is.EqualTo(new[] { "scene" }));
            Assert.That(back.FindGroup("game"), Is.Null);
            Assert.That(documents.ActiveIndex, Is.EqualTo(0), "the tab that was active went with it, so the first stands in");
        });
    }

    /// <summary>An id the application cannot produce any more is dropped on LOAD, and a group left empty by that goes
    /// with it - a layout must never name a pane nobody can make.</summary>
    [Test]
    public void PanesTheApplicationNoLongerHas_AreDroppedOnLoad()
    {
        var text = DockingLayoutSerializer.Save(Editor());
        var back = DockingLayoutSerializer.Load(text, knownPane: id => id != "inspector" && id != "hierarchy");

        Assert.Multiple(() =>
        {
            Assert.That(back.FindGroup("scene"), Is.Not.Null, "what is still there is placed");
            Assert.That(back.FindGroup("inspector"), Is.Null, "what is gone is not");
            Assert.That(back.Main.Content, Is.InstanceOf<PaneGroupNode>(),
                "and the split that held the two of them collapsed to the one child left");
        });
    }

    /// <summary>The same round trip through the AREA, which is how an application does it: the view model asks its
    /// workspace to save, the user rearranges everything, and restoring puts the panels back where they were.</summary>
    [Test]
    public void AnAreaSavesAndRestoresWhatIsOnScreen()
    {
        var area = new DockingArea { DividerThickness = 0 };
        area.Children.Add(AreaGroup("documents", DockZone.Center, "scene"));
        area.Children.Add(AreaGroup("tools", DockZone.Right, "inspector", "hierarchy"));

        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));

        // The arrangement worth remembering: the tools put away against their edge, looking at the second tab.
        var tools = area.Layout.FindGroup("inspector");
        tools.ActiveIndex = 1;
        Assert.That(area.Layout.CollapseGroup(tools), Is.True);
        area.Rebuild();

        var saved = area.SaveLayout();
        Assert.That(saved, Is.Not.Null.And.Not.Empty);

        // Now undo all of it by hand, as a user would.
        Assert.That(area.Layout.ExpandGroup(area.Layout.FindGroup("inspector")), Is.True);
        area.Layout.FindGroup("inspector").ActiveIndex = 0;
        area.Rebuild();

        Assert.That(area.LoadLayout(saved), Is.True);

        var restored = area.Layout.FindGroup("inspector");

        Assert.Multiple(() =>
        {
            Assert.That(restored.State, Is.EqualTo(PaneGroupState.Collapsed), "put away again");
            Assert.That(area.Layout.Main.EdgeOfBarred(restored), Is.EqualTo(DockZone.Right), "on its own edge");
            Assert.That(restored.ActiveIndex, Is.EqualTo(1), "looking at the tab it was looking at");
            Assert.That(area.Layout.FindGroup("scene"), Is.Not.Null, "and the documents are still there");
        });
    }

    /// <summary>A pane opened by CODE does not exist at start-up, so a layout naming it would drop it - which is how a
    /// restored arrangement ends up with the authored panels and nothing else. The file carries what it takes to make
    /// it again, and the application is asked for it before the layout is applied.</summary>
    [Test]
    public void APaneOpenedByCode_IsRemadeFromItsRestoreKey()
    {
        var before = new DockingArea { DividerThickness = 0 };
        before.Children.Add(AreaGroup("documents", DockZone.Center, "scene"));
        before.Measure(new Size(1000, 800));
        before.Arrange(new Rect(0, 0, 1000, 800));

        before.AddPane(new Pane { Header = "Assets", Id = "Assets", RestoreKey = "page:Assets" });
        before.Measure(new Size(1000, 800));
        before.Arrange(new Rect(0, 0, 1000, 800));

        var saved = before.SaveLayout();
        Assert.That(saved, Does.Contain("page:Assets"), "the file says what it takes to make that pane again");

        // A fresh area, as after a restart: only what the markup declares exists.
        var after = new DockingArea { DividerThickness = 0 };
        after.Children.Add(AreaGroup("documents", DockZone.Center, "scene"));
        after.Measure(new Size(1000, 800));
        after.Arrange(new Rect(0, 0, 1000, 800));

        var asked = "";
        after.PaneRestoring += (_, e) =>
        {
            asked = e.RestoreKey;
            e.Pane = new Pane { Header = e.PaneId, Id = e.PaneId, RestoreKey = e.RestoreKey };
        };

        Assert.That(after.LoadLayout(saved), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(asked, Is.EqualTo("page:Assets"), "the application was asked with the key it wrote");
            Assert.That(after.Layout.FindGroup("Assets"), Is.Not.Null, "and the pane is back in the layout");
            Assert.That(after.Layout.FindGroup("Assets").PaneIds, Does.Contain("scene"),
                "in the very group it was saved in");
        });
    }

    /// <summary>An application that cannot make the pane leaves it null, and the layout is applied without it - the
    /// same as before there was any way to ask.</summary>
    [Test]
    public void APaneTheApplicationCannotRemake_IsSimplyLeftOut()
    {
        var before = new DockingArea { DividerThickness = 0 };
        before.Children.Add(AreaGroup("documents", DockZone.Center, "scene"));
        before.Measure(new Size(1000, 800));
        before.Arrange(new Rect(0, 0, 1000, 800));
        before.AddPane(new Pane { Header = "Assets", Id = "Assets", RestoreKey = "page:Assets" });

        var saved = before.SaveLayout();

        var after = new DockingArea { DividerThickness = 0 };
        after.Children.Add(AreaGroup("documents", DockZone.Center, "scene"));
        after.Measure(new Size(1000, 800));
        after.Arrange(new Rect(0, 0, 1000, 800));
        after.PaneRestoring += (_, e) => e.Pane = null;

        Assert.Multiple(() =>
        {
            Assert.That(after.LoadLayout(saved), Is.True, "the rest of the arrangement still loads");
            Assert.That(after.Layout.FindGroup("Assets"), Is.Null);
            Assert.That(after.Layout.FindGroup("scene"), Is.Not.Null);
        });
    }

    private static PaneGroup AreaGroup(string name, DockZone zone, params string[] panes)
    {
        var group = new PaneGroup { Name = name, Zone = zone };
        foreach (var pane in panes) group.Items.Add(new Pane { Header = pane, Id = pane, Kind = PaneKind.Tool });

        return group;
    }

    [Test]
    public void TextThisVersionCannotRead_IsRefusedRatherThanThrown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DockingLayoutSerializer.Load(null), Is.Null);
            Assert.That(DockingLayoutSerializer.Load(""), Is.Null);
            Assert.That(DockingLayoutSerializer.Load("{ this is not json"), Is.Null, "a corrupt file is not a crash");
            Assert.That(DockingLayoutSerializer.Load("{\"version\":999,\"roots\":[]}"), Is.Null, "nor is a newer one");
        });
    }
}
