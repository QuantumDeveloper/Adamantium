using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// What a tab's context menu is built from. The MENU belongs to the application - it holds saving, source control,
/// whatever that application has - but the operations do not: without them the application would walk the layout by
/// hand, and a second way to close a pane is a second set of rules about what closing means.
/// <para>The rule they all share: one pane at a time, through the same path as the tab's own close button. So a refusal
/// stops THAT pane and no other, and pinned tabs survive what was aimed at the rest.</para>
/// </summary>
[TestFixture]
public class DockingCloseCommandsTests
{
    private static DockingArea Area(params string[] documents)
    {
        var group = new PaneGroup { Name = "documents", Zone = DockZone.Center };
        foreach (var id in documents) group.Items.Add(new Pane { Header = id, Id = id, Kind = PaneKind.Document });

        var area = new DockingArea { DividerThickness = 0 };
        area.Children.Add(group);
        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));
        return area;
    }


    [Test]
    public async Task ClosingEveryTabOfAPanel_LeavesNoneOfThemOpen()
    {
        var area = Area("scene", "game", "profiler");

        Assert.That(await area.ClosePanesOfGroupAsync("game"), Is.EqualTo(3), "all three went");
        Assert.That(area.Layout.FindGroup("scene"), Is.Null, "and none of them is in the layout");
    }

    [Test]
    public async Task ClosingTheOthers_KeepsTheOneItWasAskedFrom()
    {
        var area = Area("scene", "game", "profiler");

        Assert.That(await area.CloseOtherPanesAsync("game"), Is.EqualTo(2));

        var open = area.Layout.FindGroup("game").PaneIds;
        Assert.That(open, Is.EqualTo(new[] { "game" }), "only the one the menu was opened on is left");
    }

    /// <summary>A pinned tab is exactly the one that must not go - that is what pinning it was for.</summary>
    [Test]
    public async Task ClosingTheUnpinned_LeavesThePinnedOnesAlone()
    {
        var area = Area("scene", "game", "profiler");
        area.PaneById("scene").IsPinned = true;

        Assert.That(await area.CloseUnpinnedPanesAsync("game"), Is.EqualTo(2));

        var open = area.Layout.FindGroup("scene").PaneIds;
        Assert.That(open, Is.EqualTo(new[] { "scene" }), "the pinned tab stayed, the rest went");
    }

    /// <summary>The refusal is per PANE: the document with unsaved work stays, everything else still closes. Neither
    /// "one no cancels the lot" nor "the lot ignores the no".</summary>
    [Test]
    public async Task ARefusalStopsThatPaneAndNoOther()
    {
        var area = Area("scene", "game", "profiler");
        area.PaneClosing += (_, e) => { e.Cancel = e.PaneId == "game"; return Task.CompletedTask; };

        Assert.That(await area.ClosePanesOfGroupAsync("scene"), Is.EqualTo(2), "two of the three closed");

        var open = area.Layout.FindGroup("game").PaneIds;
        Assert.That(open, Is.EqualTo(new[] { "game" }), "and the one that said no is still open");
    }

    /// <summary>Every close goes through the same door, so a single close is refusable too - not only a bulk one.</summary>
    [Test]
    public async Task ARefusedSingleClose_LeavesThePaneWhereItWas()
    {
        var area = Area("scene", "game");
        area.PaneClosing += (_, e) => { e.Cancel = true; return Task.CompletedTask; };

        var closedAnnounced = false;
        area.PaneClosed += (_, _) => closedAnnounced = true;

        var closed = await area.ClosePaneAsync("game");

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.False, "it says it did not close");
            Assert.That(area.Layout.FindGroup("game"), Is.Not.Null, "the pane is still in the layout");
            Assert.That(closedAnnounced, Is.False, "and nothing was announced as closed");
        });
    }

    /// <summary>The handler may WAIT - that is the whole reason it returns a Task - and the area waits with it. Without
    /// this, an application could only refuse out of what it already knows, never out of what it asks the user.</summary>
    [Test]
    public async Task AHandlerThatTakesItsTime_IsWaitedFor()
    {
        var area = Area("scene", "game");
        area.PaneClosing += async (_, e) =>
        {
            await Task.Delay(20);            // stands in for a dialog being answered
            e.Cancel = e.PaneId == "game";
        };

        Assert.Multiple(async () =>
        {
            Assert.That(await area.ClosePaneAsync("game"), Is.False, "the late answer still counted");
            Assert.That(await area.ClosePaneAsync("scene"), Is.True, "and a late yes closes as usual");
        });
    }

    /// <summary>"Cancel" in a save-before-closing dialog means STOP, not "keep this one": the panes after it are not
    /// asked about at all.</summary>
    [Test]
    public async Task CancelAll_StopsTheRestOfTheOperation()
    {
        var area = Area("scene", "game", "profiler");

        var asked = new System.Collections.Generic.List<string>();
        area.PaneClosing += (_, e) =>
        {
            asked.Add(e.PaneId);
            if (e.PaneId == "game") e.CancelAll = true;
            return Task.CompletedTask;
        };

        var closed = await area.ClosePanesOfGroupAsync("scene");

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.EqualTo(1), "only the one before the stop went");
            Assert.That(asked, Is.EqualTo(new[] { "scene", "game" }), "and 'profiler' was never even asked about");
        });
    }

    [Test]
    public async Task ClosingEverything_EmptiesTheWholeLayout()
    {
        var area = Area("scene", "game", "profiler");

        Assert.That(await area.CloseAllPanesAsync(), Is.EqualTo(3));
        Assert.That(area.Panes, Is.Empty, "nothing is open anywhere in the layout");
    }
}
