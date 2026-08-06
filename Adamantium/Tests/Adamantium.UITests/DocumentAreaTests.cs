using System.Linq;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Rule 1.6: the document area splits WITHIN ITSELF and every part of it stays a document. A tool may be dropped into
/// the area, but the area itself is never dragged into a tool - the centre of editing is a PLACE, not a panel.
/// </summary>
[TestFixture]
public class DocumentAreaTests
{
    private static DockingLayout Editor(params string[] documents)
    {
        var well = new PaneGroupNode();
        foreach (var id in documents) well.Add(id);

        var tools = new PaneGroupNode();
        tools.Add("inspector");

        return DockingLayout.FromZones(
        [
            new ZoneDeclaration(DockZone.Center, well, double.NaN),
            new ZoneDeclaration(DockZone.Right, tools, 240)
        ]);
    }

    /// <summary>A document dropped on the SIDE of the document area gives two editors side by side, and the area is
    /// now the split that holds them - not the group it started as.</summary>
    [Test]
    public void ADropIntoTheArea_SplitsTheAreaItself()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");

        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);

        var moved = layout.FindGroup("game");

        Assert.Multiple(() =>
        {
            Assert.That(layout.DocumentWell, Is.InstanceOf<PaneSplitNode>(), "the area became a split of its own");
            Assert.That(layout.IsDocument(documents), Is.True, "the group it started as is still part of it");
            Assert.That(layout.IsDocument(moved), Is.True, "and so is what landed beside it");
            Assert.That(moved.Parent, Is.SameAs(layout.DocumentWell), "both sit directly in the area");
        });
    }

    /// <summary>Both halves are documents, whatever they were before: a TOOL dropped into the area is one too, which
    /// is rule 1.2 read through the new definition of "inside".</summary>
    [Test]
    public void ATOOLDroppedIntoTheArea_BecomesPartOfIt()
    {
        var layout = Editor("scene");
        var documents = layout.FindGroup("scene");

        Assert.That(layout.MovePane("inspector", documents, DockZone.Bottom), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.IsDocument(layout.FindGroup("inspector")), Is.True);
            Assert.That(layout.IsDocument(layout.FindGroup("scene")), Is.True);
        });
    }

    /// <summary>A drop AGAINST the area from outside - what an edge anchor does - is not a split of the area: the tool
    /// lands beside it and stays a tool.</summary>
    [Test]
    public void ADropAgainstTheRoot_LeavesTheAreaAlone()
    {
        var layout = Editor("scene");
        var wellBefore = layout.DocumentWell;
        var root = layout.Main.Content;

        Assert.That(layout.MovePane("inspector", root, DockZone.Left, size: PaneLength.Pixels(240)), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.DocumentWell, Is.SameAs(wellBefore), "the area is what it was");
            Assert.That(layout.IsDocument(layout.FindGroup("inspector")), Is.False, "and the tool is outside it");
        });
    }

    /// <summary>The area itself never moves: it is a place. Everything IN it may leave, including the last group - the
    /// place stays behind, empty, ready to be opened into (rule 1.4).</summary>
    [Test]
    public void TheAreaStaysPut_WhileEverythingInItMayLeave()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");

        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);
        var second = layout.FindGroup("game");

        Assert.That(layout.TearOffGroup(second), Is.Not.Null, "one of two editors may leave");
        Assert.That(layout.IsDocument(layout.FindGroup("scene")), Is.True, "and the area is still there");

        Assert.That(layout.TearOffGroup(layout.FindGroup("scene")), Is.Not.Null, "and so may the last one");

        Assert.Multiple(() =>
        {
            Assert.That(layout.DocumentWell, Is.Not.Null, "the place is still there");
            Assert.That(DockingLayout.PanesIn(layout.DocumentWell), Is.Empty, "with nothing in it");
            Assert.That(layout.Main.Content, Is.Not.Null, "and the main window still has its layout");
        });
    }

    /// <summary>No part of the document area folds away: there is no edge in the centre to fold against.</summary>
    [Test]
    public void NoPartOfTheArea_CanBeFoldedAway()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");
        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.CollapseGroup(layout.FindGroup("scene")), Is.False);
            Assert.That(layout.CollapseGroup(layout.FindGroup("game")), Is.False);
            Assert.That(layout.CollapseGroup(layout.FindGroup("inspector")), Is.True, "a tool still folds");
        });
    }

    /// <summary>A new document opens into the ACTIVE group of the area, not into whatever the area started as.</summary>
    [Test]
    public void ANewDocumentGoesToTheActiveGroupOfTheArea()
    {
        var layout = Editor("scene");
        var documents = layout.FindGroup("scene");
        Assert.That(layout.MovePane("inspector", documents, DockZone.Right), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.ActiveWellGroup(null), Is.Not.Null);
            Assert.That(layout.IsDocument(layout.ActiveWellGroup(null)), Is.True, "and it is inside the area");
        });
    }

    /// <summary>Split the documents in two and a new document must open in the half being WORKED IN, not in whichever
    /// half the structure lists first. Measured before the fix: the active pane was tracked but never asked, so
    /// ActiveWellGroup answered "the first non-empty group" and every new tab landed in the left half.</summary>
    [Test]
    public void ANewDocumentGoesToTheSplitTheUserIsWorkingIn()
    {
        var layout = Editor("left", "right");
        var leftGroup = layout.FindGroup("left");

        // Split the document area: "right" moves out of the shared group into a second one beside it.
        Assert.That(layout.MovePane("right", leftGroup, DockZone.Right), Is.True);
        var rightGroup = layout.FindGroup("right");
        Assert.That(rightGroup, Is.Not.SameAs(leftGroup), "the area must actually be split in two");

        Assert.Multiple(() =>
        {
            Assert.That(layout.ActiveWellGroup("right"), Is.SameAs(rightGroup), "active on the right -> the right half");
            Assert.That(layout.ActiveWellGroup("left"), Is.SameAs(leftGroup), "active on the left -> the left half");
            // A tool (or nothing) is active: no half is being worked in, so the old behaviour stands.
            Assert.That(layout.ActiveWellGroup(null), Is.SameAs(leftGroup), "nothing active -> the first group");
            Assert.That(layout.ActiveWellGroup("inspector"), Is.SameAs(leftGroup), "a TOOL active -> the first group");
        });
    }

    /// <summary>A document carried out into a window of its own is STILL a document: it keeps document chrome while it
    /// floats, does not fold away, and docks back as a document. Measured before the rule: every torn-off editor came
    /// out wearing a tool panel's caption, because "document" was read as "in the well right now".</summary>
    [Test]
    public void ADocumentTornOffIntoItsOwnWindow_IsStillADocument()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");
        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);

        var torn = layout.TearOffGroup(layout.FindGroup("game"));

        Assert.Multiple(() =>
        {
            Assert.That(torn, Is.Not.Null);
            Assert.That(torn.DocumentWell, Is.Not.Null, "the window has a document area of its own");
            Assert.That(layout.IsDocument(layout.FindGroup("game")), Is.True, "so what is in it is a document");
            Assert.That(layout.CollapseGroup(layout.FindGroup("game")), Is.False, "and it still does not fold away");
        });
    }

    /// <summary>A TOOL carried out into a window of its own stands in THAT window's centre, and a centre belongs to
    /// documents (rule 1.2) - so it is dressed as one while it stands there alone, and a document dropped in beside it
    /// is a document too. Dock something to its SIDE and both are tools again, which is the only thing that makes one.
    /// <para>Measured before the rule: a window born of a tool stayed a tool window forever, and a document dropped into
    /// it turned into a tool - the same drop meaning two different things depending on which window it landed in.</para>
    /// </summary>
    [Test]
    public void ATOOLCarriedIntoItsOwnWindow_StandsInThatWindowsCentre()
    {
        var layout = Editor("scene", "game");

        var torn = layout.TearOffGroup(layout.FindGroup("inspector"));

        Assert.Multiple(() =>
        {
            Assert.That(torn, Is.Not.Null);
            Assert.That(layout.IsDocument(layout.FindGroup("inspector")), Is.True, "alone in a window, it is a document");
            Assert.That(layout.IsDocument(layout.FindGroup("scene")), Is.True, "and home is unchanged");
        });

        // Something docked BESIDE it out there: now there is a centre and a side, and the side is a tool.
        Assert.That(layout.MovePane("game", torn.Content, DockZone.Right, size: PaneLength.Pixels(240), beside: true),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.IsDocument(layout.FindGroup("inspector")), Is.True, "the centre is still the centre");
            Assert.That(layout.IsDocument(layout.FindGroup("game")), Is.False, "what went to the side is a tool");
        });
    }

    /// <summary>A window has its own document AREA, not a "these are documents" badge across the whole of it: a tool
    /// docked BESIDE the editor out there is still a tool, and one dropped INTO it is a document - the same rule 1.6
    /// that holds at home.
    /// <para>Measured before this: the window carried a flag, so everything in it came out with document chrome - the
    /// inspector included, caption and pin gone.</para></summary>
    [Test]
    public void ATOOLDockedBesideADocumentInItsWindow_StaysATool()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");
        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);

        var torn = layout.TearOffGroup(layout.FindGroup("game"));
        var editor = torn.Content;

        // Docked against the whole window, which is what an edge anchor does - beside the area, not into it.
        Assert.That(layout.MovePane("inspector", editor, DockZone.Right, size: PaneLength.Pixels(240), beside: true),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.IsDocument(layout.FindGroup("game")), Is.True, "the editor is still a document");
            Assert.That(layout.IsDocument(layout.FindGroup("inspector")), Is.False, "and the tool beside it is not");
            Assert.That(layout.CollapseGroup(layout.FindGroup("inspector")), Is.True, "so it still folds away");
        });
    }

    /// <summary>A floating window that has been SPLIT docks back whole: dropped on a centre indicator, everything it
    /// holds becomes tabs of the target. Refused, the only way back was one tab at a time.</summary>
    [Test]
    public void AWindowHoldingASplit_DocksBackWholeOntoTheCentre()
    {
        var layout = Editor("scene", "game", "console");
        var documents = layout.FindGroup("scene");
        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);

        var torn = layout.TearOffGroup(layout.FindGroup("game"));
        Assert.That(layout.MovePane("console", torn.Content, DockZone.Bottom), Is.True, "and split in the window");
        Assert.That(torn.Content, Is.InstanceOf<PaneSplitNode>());

        Assert.That(layout.MoveNode(torn.Content, layout.FindGroup("scene"), DockZone.Center), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FindGroup("scene").PaneIds, Is.EqualTo(new[] { "scene", "game", "console" }));
            Assert.That(layout.Roots, Has.Count.EqualTo(1), "and the window it came from is gone");
        });
    }

    /// <summary>Every document carried out into ONE window and then dropped back lands as the tabs it was: emptying the
    /// area leaves an empty group in it, and dropping onto that group must fill it - not replace it with one pane.</summary>
    [Test]
    public void EveryDocumentCarriedOutAndBackAgain_ComesBackWhole()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");

        // Both out, into the SAME window: the first tears off, the second joins it.
        var torn = layout.TearOffGroup(documents);
        Assert.That(torn, Is.Not.Null);

        var emptied = layout.DocumentWell as PaneGroupNode;
        Assert.That(emptied, Is.Not.Null, "the place stays behind");
        Assert.That(emptied.IsEmpty, Is.True);

        // ...and back, all at once.
        Assert.That(layout.MoveNode(torn.Content, emptied, DockZone.Center), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FindGroup("scene"), Is.SameAs(emptied), "into the place that was waiting");
            Assert.That(emptied.PaneIds, Is.EqualTo(new[] { "scene", "game" }), "with everything it was carrying");
            Assert.That(layout.Roots, Has.Count.EqualTo(1), "and the window is gone");
        });
    }

    /// <summary>Emptying one of two editors removes it, and the area collapses back to the one that is left - the same
    /// tidying any split gets. What must NOT happen is the area disappearing with its last group.</summary>
    [Test]
    public void EmptyingOneEditor_LeavesTheAreaWithTheOther()
    {
        var layout = Editor("scene", "game");
        var documents = layout.FindGroup("scene");
        Assert.That(layout.MovePane("game", documents, DockZone.Right), Is.True);

        Assert.That(layout.RemovePane("game"), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(layout.FindGroup("scene"), Is.Not.Null);
            Assert.That(layout.IsDocument(layout.FindGroup("scene")), Is.True, "what is left is still the document area");
        });

        Assert.That(layout.RemovePane("scene"), Is.True);
        Assert.That(layout.DocumentWell, Is.Not.Null, "and emptying it entirely leaves the area as empty space");
    }
}
