using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Where a newcomer's draw goes in an already-recorded frame (§5a). A recorded batch segment glues every control that fell
/// between two flushes, and what makes that legal is that none of them overlaps another: inside such a set the paint order
/// simply does not matter. So a control that starts drawing needs a place of its OWN only when it overlaps what the set
/// draws - then order decides what covers what. When it does not overlap, it joins the set and the set stays whole.
/// <para>Both halves are pinned here, and both by the same question - does the patched frame equal what a full walk draws -
/// because a cut avoided by mistake and a cut taken by mistake look identical in a segment count and opposite on screen.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class LayerPlacementRenderTests
{
    private const int Dim = 96;
    private const int Cards = 3;
    private const int CardHeight = 10;
    private const int FarY = 70;      // below everything the cards cover - a different region of the window

    private sealed class Scene : IDisposable
    {
        public OffscreenTestRenderer Renderer;
        public VisualRoot Root;
        // Both draw nothing until asked, and both are ranked BETWEEN the cards - what differs is WHERE they are: a control's
        // own footprint is what the placement asks about (a patch is described by the component, not by the rect it happened
        // to draw), so "away" and "over" have to be two different controls.
        public TestControl[] Far;
        public TestControl[] Over;
        public TestControl[] Cards;
        public TestControl Stage;

        public void Draw()
        {
            Assert.That(Renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();
        }

        public void Dispose() => Renderer.Dispose();
    }

    private static TestControl Placed(Rect bounds) =>
        new() { Bounds = bounds, RenderSize = new Size(bounds.Width, bounds.Height) };

    // Cards stacked in the TOP band (disjoint, so one segment holds them all), each followed by an empty sibling whose rank
    // therefore lands INSIDE that segment's paint span - which is what makes it a placement question at all.
    private static Scene NewScene()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var cards = new TestControl[Cards];
        var far = new TestControl[Cards];
        var over = new TestControl[Cards];
        for (var i = 0; i < Cards; i++)
        {
            var y = i * (CardHeight + 2);
            cards[i] = Placed(new Rect(0, y, Dim, CardHeight));
            cards[i].RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(4, 0, Dim - 8, CardHeight));
            stage.Add(cards[i]);

            far[i] = Placed(new Rect(0, FarY, Dim, CardHeight));
            stage.Add(far[i]);

            over[i] = Placed(new Rect(0, y, 20, CardHeight));
            stage.Add(over[i]);
        }

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Far = far, Over = over, Cards = cards, Stage = stage };
        scene.Draw();
        return scene;
    }

    private static byte[] Pixels(OffscreenTestRenderer renderer)
    {
        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    private static int DifferingPixels(byte[] a, byte[] b)
    {
        var count = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) count++;
        }

        return count;
    }

    private static void AssertMatchesAFullWalk(Scene scene, byte[] patched, string because)
    {
        RenderDirty.MarkStructural();
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.False, "the reference frame has to actually walk");
        Assert.That(DifferingPixels(patched, Pixels(scene.Renderer)), Is.Zero, because);
    }

    // A newcomer in ANOTHER region of the window: its rank lands inside the cards' segment, but it touches none of them, so
    // its order relative to them cannot matter - and cutting the segment at its rank buys nothing. Before the overlap test,
    // the rank alone decided and every such placement cost a cut.
    [Test]
    public void ANewcomerAwayFromTheLayer_JoinsItWithoutCuttingIt()
    {
        using var scene = NewScene();

        LayerProbe.Reset();
        scene.Far[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
        scene.Far[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(LayerProbe.Splits, Is.Zero, "nothing it draws is covered by that layer - there is nothing to cut it for");
        Assert.That(LayerProbe.SplitsAvoided, Is.GreaterThan(0), "...and the placement did consider that layer");
        AssertMatchesAFullWalk(scene, patched, "joining a layer must draw what a full walk draws");
    }

    // ...and the other half: a newcomer that DOES cover part of a card. Now order is the whole question - it paints after
    // that card and must cover it - so the layer is cut at its rank, exactly as before.
    [Test]
    public void ANewcomerOverTheLayer_CutsItAndLandsOnTop()
    {
        using var scene = NewScene();

        LayerProbe.Reset();
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 2, 20, CardHeight - 4));
        scene.Over[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(LayerProbe.Splits, Is.GreaterThan(0), "it covers a card in that layer, so the layer has to be cut at its rank");
        AssertMatchesAFullWalk(scene, patched, "the newcomer must land exactly where a full walk puts it");
    }

    // The one that must hold whatever the placement decided: a newcomer away from the layer, then one over it, in the same
    // recorded frame's lifetime. Repeated laps, because the arena hands freed blocks back out and the second lap gets a
    // range somebody else used.
    [Test]
    public void PlacementsOfBothKinds_LeaveTheFrameEqualToAWalk()
    {
        using var scene = NewScene();

        for (var lap = 0; lap < 3; lap++)
        {
            scene.Far[0].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
            scene.Far[0].Invalidate();
            scene.Draw();

            scene.Over[2].RenderAction = s => s.DrawRectangle(Brushes.Yellow, new Rect(0, 2, 16, CardHeight - 4));
            scene.Over[2].Invalidate();
            scene.Draw();

            var patched = Pixels(scene.Renderer);
            AssertMatchesAFullWalk(scene, patched, $"after lap {lap} the patched frame must equal a walk");

            scene.Far[0].RenderAction = null;
            scene.Far[0].Invalidate();
            scene.Over[2].RenderAction = null;
            scene.Over[2].Invalidate();
            scene.Draw();
        }
    }

    // A control that STOPS drawing - hidden, but still in the tree, which is what a scrollbar does when the window grows
    // enough not to need it - keeps its units on purpose: a re-show must not rebuild them. What it must NOT keep is a place
    // in the picture. Its instances stay inside the layer's retained range, and a later patch re-issues that range as
    // BYTES, which puts the hidden control back on screen where it last drew. Reported from the live app: maximise the
    // window, then hover the list, and the scroll TRACK reappears down the middle of it.
    [Test]
    public void AControlThatStoppedDrawing_DoesNotComeBackWhenItsLayerIsReissued()
    {
        using var scene = NewScene();

        // It draws (the "scrollbar" is there while the window is small), with content on BOTH sides of it in the layer -
        // the live case is a track in the middle of a list, not at its end.
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        scene.Far[2].RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(4, 0, 20, CardHeight));
        scene.Far[2].Invalidate();
        scene.Draw();

        // ...and then it does not (the window grew; the control stays in the tree).
        scene.Over[1].Visibility = Visibility.Collapsed;
        scene.Over[1].Invalidate();
        scene.Draw();

        // A neighbour EARLIER in paint order changes - the hover that re-issues the layer those instances live in.
        scene.Cards[0].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, Dim - 8, CardHeight));
        scene.Cards[0].Invalidate();
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "re-issuing a layer must not bring back a control that stopped drawing");
    }

    // The other way a control stops drawing, and the one a tab switch takes: it LEAVES THE TREE. Nothing walks it after
    // that, so nothing re-records it - and its instances stay inside the layer's retained range, which a later patch
    // re-issues as BYTES. The view that left then comes back on screen, frozen at the size it had when it left. Reported
    // from the live app as a scroll track belonging to another tab's list, drawn at the previous window's size.
    [Test]
    public void AControlRemovedFromTheTree_DoesNotComeBackWhenItsLayerIsReissued()
    {
        using var scene = NewScene();

        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        scene.Far[2].RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(4, 0, 20, CardHeight));
        scene.Far[2].Invalidate();
        scene.Draw();

        // Out of the tree, and not parked: nobody is coming back to it.
        scene.Stage.Remove(scene.Over[1]);
        scene.Draw();

        // A neighbour EARLIER in paint order changes - the patch that re-issues the layer those instances live in.
        scene.Cards[0].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, Dim - 8, CardHeight));
        scene.Cards[0].Invalidate();
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a control that left the tree must not be drawn by a later patch");
    }

    // The live shape of the same thing, and the one that stayed broken: a whole VIEW leaves the tree, and what draws is
    // not the view but the parts INSIDE it. The removal names the view; its children are reached by walking the subtree,
    // and each is kept only if the recorder still remembers holding units for it or it still has a paint rank. A part that
    // has neither is skipped - and the applier goes on drawing it from the retained range, frozen at the size it had when
    // its tab was left.
    [Test]
    public void ASubtreeRemovedFromTheTree_TakesWhatItsPartsDrawWithIt()
    {
        using var scene = NewScene();

        var view = Placed(new Rect(0, 20, Dim, CardHeight));
        var part = Placed(new Rect(0, 0, 20, CardHeight));
        part.RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        view.Add(part);
        scene.Stage.Add(view);
        scene.Draw();

        // The tab is switched: the view leaves whole, with its parts inside it.
        scene.Stage.Remove(view);
        scene.Draw();

        // ...and a neighbour EARLIER in paint order changes - the patch that re-issues the range those instances live in.
        scene.Cards[0].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, Dim - 8, CardHeight));
        scene.Cards[0].Invalidate();
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "what a removed view's parts drew must go with the view");
    }

    // The live one, and the reason removing a view is not enough on its own: RECORD and APPLY are separate halves that run
    // a frame apart. A packet recorded while the view was still in the tree lands AFTER it left, and realizing its draws
    // puts the view back into the paint order - behind the back of everything that withdraws what left. The retained op
    // stream then re-issues it for as long as the frame stays clean: the tab that was left, drawn over the tab that
    // replaced it, frozen at the size it had when it went.
    [Test]
    public void APacketRecordedBeforeAViewLeft_DoesNotPutItBackWhenItLands()
    {
        using var scene = NewScene();

        var view = Placed(new Rect(0, 20, Dim, CardHeight));
        var part = Placed(new Rect(0, 0, 20, CardHeight));
        part.RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        view.Add(part);
        scene.Stage.Add(view);
        scene.Draw();

        // In flight: recorded with the view in the tree...
        part.Invalidate();
        scene.Renderer.Cache.RecordFrame(scene.Root);

        // ...the tab is switched while it travels...
        scene.Stage.Remove(view);

        // ...and it lands here, on a tree that no longer holds the view.
        scene.Renderer.Cache.ApplyFrame();
        scene.Draw();

        // A neighbour EARLIER in paint order changes - the patch that re-issues the range those instances live in.
        scene.Cards[0].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, Dim - 8, CardHeight));
        scene.Cards[0].Invalidate();
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a packet that landed late must not put a departed view back on screen");
    }

    // The live one, caught in the app: a control stops drawing (its ScrollBar collapses when the window grows enough not
    // to need it) and the frames that follow are pure REPLAYS - no patch, nothing dirty, the retained op stream re-issued
    // as it stands. Its instances sit INSIDE a segment other controls still use, and a segment is issued as a RANGE, so
    // the range carries them along: the bar goes on being drawn, frozen at the size it had when it was last needed.
    // Patching a neighbour rewrites that range and hides the fault - which is why the sibling test above passes.
    [Test]
    public void AControlThatStoppedDrawing_IsGoneFromAPlainREPLAY()
    {
        using var scene = NewScene();

        // It draws, between cards, so its slots land in the middle of the layer's range rather than at its end.
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        scene.Draw();

        // ...and then it does not: the window grew, the bar is not needed (the control stays in the tree, collapsed).
        scene.Over[1].Visibility = Visibility.Collapsed;
        scene.Draw();

        // The next frame changes NOTHING - exactly the idle frame the app spends its life in.
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "this test is about the REPLAY path - it has to replay");

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a replayed frame must not carry a control that stopped drawing");
    }

    // The live shape, which every test above missed: what is COLLAPSED is the parent, and what DRAWS is the child inside
    // it. A ScrollBar that is no longer needed collapses; the Border that paints its track is still Visible in its own
    // right and never hears about it. Nothing re-records a subtree that is not walked any more, so the child's instances
    // stay inside the segment range they were recorded in - and a replayed frame issues that range whole.
    [Test]
    public void AChildOfACollapsedParent_IsGoneFromAReplayedFrame()
    {
        using var scene = NewScene();

        // The "bar": a parent that draws nothing itself, with a child that paints it - between the cards, so its slots
        // land in the middle of the layer's range.
        var bar = Placed(new Rect(0, 12, 20, CardHeight));
        var track = Placed(new Rect(0, 0, 12, CardHeight));
        track.RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        bar.Add(track);
        scene.Stage.Add(bar);
        scene.Draw();

        // No longer needed: the PARENT collapses. The child keeps its own Visibility, as a template part does.
        bar.Visibility = Visibility.Collapsed;
        scene.Draw();

        // ...and the frames that follow change nothing at all.
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a collapsed parent must take what its children draw with it");
    }

    // What the app does that none of the tests above do: the control goes on NOT being re-recorded while the arena around
    // it is re-laid many times over. Measured live, its instances moved from slot 11 to 29 to 63 to 302 while the group
    // itself was last written thousands of frames earlier - so by the time it stops drawing, everything the cache
    // REMEMBERS about where its slots are (its runs) names somebody else's. A withdrawal that goes by those remembered
    // addresses then blanks the wrong place and reports success, and the control keeps painting.
    [Test]
    public void AControlThatStoppedDrawing_IsGone_EvenAfterTheArenaMovedItsSlotsAround()
    {
        using var scene = NewScene();

        // It draws, between cards - the track in the middle of a list, not at its end.
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        scene.Draw();

        // Laps of unrelated edits: neighbours start and stop drawing, which re-lays the arena underneath the bar and
        // hands its old slots to other controls. The bar itself is untouched throughout - nothing re-records it.
        for (var lap = 0; lap < 4; lap++)
        {
            scene.Far[lap % scene.Far.Length].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
            scene.Far[lap % scene.Far.Length].Invalidate();
            scene.Draw();

            scene.Cards[lap % Cards].RenderAction = s => s.DrawRectangle(Brushes.Yellow, new Rect(2, 0, Dim - 4, CardHeight));
            scene.Cards[lap % Cards].Invalidate();
            scene.Draw();

            scene.Far[lap % scene.Far.Length].RenderAction = null;
            scene.Far[lap % scene.Far.Length].Invalidate();
            scene.Draw();
        }

        // Only now does it stop drawing - the window grew and the bar is not needed.
        scene.Over[1].Visibility = Visibility.Collapsed;
        scene.Over[1].Invalidate();
        scene.Draw();

        // ...and the frames that follow are the idle ones the app spends its life in.
        scene.Draw();
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "the frames after it goes have to be replays");

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a control that stopped drawing must go, however far its slots have travelled since it last drew");
    }

    // The same, for a control that LEAVES THE TREE rather than collapsing - the tab switch - after the arena has moved
    // on. Separate test because the two take different routes out of the paint order, and only one of them was ever
    // covered against a stale run list.
    [Test]
    public void AControlRemovedFromTheTree_IsGone_EvenAfterTheArenaMovedItsSlotsAround()
    {
        using var scene = NewScene();

        var view = Placed(new Rect(0, 20, Dim, CardHeight));
        var part = Placed(new Rect(0, 0, 20, CardHeight));
        part.RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        view.Add(part);
        scene.Stage.Add(view);
        scene.Draw();

        for (var lap = 0; lap < 4; lap++)
        {
            scene.Far[lap % scene.Far.Length].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
            scene.Far[lap % scene.Far.Length].Invalidate();
            scene.Draw();

            scene.Far[lap % scene.Far.Length].RenderAction = null;
            scene.Far[lap % scene.Far.Length].Invalidate();
            scene.Draw();
        }

        scene.Stage.Remove(view);
        scene.Draw();
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a view that left must go, however far its slots have travelled since it last drew");
    }

    // The layers are the structure of a recorded frame - which draws may be reordered among themselves and which may not
    // (§5a) - and the stream is what a replay walks. They have to stay the same sequence: a layer list that has drifted
    // from the stream would answer "your order here does not matter" about a set the frame does not actually draw
    // together, and that is a wrong picture rather than a slow frame. Asked after each kind of edit a frame can take.
    [Test]
    public void TheLayers_KeepDescribingTheStream_ThroughEveryKindOfEdit()
    {
        using var scene = NewScene();

        Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var afterWalk), Is.True, afterWalk);

        // A newcomer that joins a layer...
        scene.Far[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
        scene.Far[1].Invalidate();
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var afterJoin), Is.True, afterJoin);

        // ...one that cuts it...
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Yellow, new Rect(0, 2, 20, CardHeight - 4));
        scene.Over[1].Invalidate();
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var afterCut), Is.True, afterCut);

        // ...and one that stops drawing.
        scene.Over[1].Visibility = Visibility.Collapsed;
        scene.Over[1].Invalidate();
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var afterHide), Is.True, afterHide);

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "and the frame still equals what a walk draws");
    }

    // The case the first version of the layer bookkeeping got wrong, and it is the COMMON one: a newcomer's place is
    // found by rank and then backed up over the scissor ops that set up the draw after it, which lands the insert exactly
    // BETWEEN two layers. Claimed by neither, the op still sits in the stream - so every later layer's window into it is
    // one op short, and the frame is assembled out of pieces of its neighbours. Live, on a theme swap (dozens of splices
    // in a row), that showed as one tab's content painted across the tab strip.
    [Test]
    public void ManySplicesInARow_LeaveEveryLayerDescribingItsOwnOps()
    {
        using var scene = NewScene();

        // NOT clipped on purpose. Turning ClipToBounds on here does make the boundary case reachable (a clipped control
        // records scissor ops, and an insertion point backed up over them lands between two layers) - and it then fails,
        // by 74 pixels, with the layer bookkeeping bypassed entirely. That is the documented limit of the flat stream
        // (see OpIndexForRank and BorderPatchRenderTests.ANeighbourAppearing_DoesNotCostABorderItsRing), not this.
        for (var lap = 0; lap < 6; lap++)
        {
            // Alternate the two kinds of placement, so inserts land both inside a layer and at its edges.
            var i = lap % Cards;
            scene.Far[i].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
            scene.Far[i].Invalidate();
            scene.Draw();
            Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var afterFar), Is.True, $"lap {lap} (away): {afterFar}");

            scene.Over[i].RenderAction = s => s.DrawRectangle(Brushes.Yellow, new Rect(0, 2, 16, CardHeight - 4));
            scene.Over[i].Invalidate();
            scene.Draw();
            Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var afterOver), Is.True, $"lap {lap} (over): {afterOver}");

            AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), $"after lap {lap} the patched frame must equal a walk");

            scene.Far[i].RenderAction = null;
            scene.Over[i].RenderAction = null;
            scene.Far[i].Invalidate();
            scene.Over[i].Invalidate();
            scene.Draw();
        }
    }

    // A CLIPPED control draws in a segment of its own (a batch is flushed when the next draw needs another scissor), so
    // when it stops drawing that segment has nobody left in it. What is pinned here is what a test CAN pin: the frame
    // that follows still equals a walk, and the layers still tile the stream exactly.
    // <para>What it deliberately does NOT claim is that the draw call itself disappeared. It does - the sweep drops a
    // segment that draws nothing - but hiding a control also marks the frame structural, and the rebuild that follows
    // re-lays the stream without it anyway. The two are indistinguishable from here: the sweep earns its keep only where
    // no rebuild follows at all, which is the live replayed frame this harness cannot produce.</para>
    [Test]
    public void AClippedControlThatStopsDrawing_LeavesTheFrameEqualToAWalk()
    {
        using var scene = NewScene();

        scene.Over[1].ClipToBounds = true;
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        RenderDirty.MarkStructural();
        scene.Draw();

        scene.Over[1].Visibility = Visibility.Collapsed;
        scene.Over[1].Invalidate();
        scene.Draw();
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LayersDescribeTheStream(out var why), Is.True, why);
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "the frame still equals what a walk draws");
    }

    // HIDING is not a structural change: the control keeps its slot, its size and its rank, and simply stops painting.
    // So the recorded frame is PATCHED - its group empties where it stands - instead of being discarded for a walk of the
    // whole window. That is what a hover affordance costs while a tab strip scrolls under a still pointer: measured on a
    // heavy tab, one close button appearing and vanishing was a full-scene walk apiece, ~105 of them in eight seconds.
    [Test]
    public void AControlThatHides_IsPatchedOutOfTheFrameInsteadOfRebuildingIt()
    {
        using var scene = NewScene();

        // A CHILD draws too - the case this is really about is a button whose glyph is a child of it, and hiding the
        // button has to take the glyph with it and bring it back.
        var glyph = Placed(new Rect(0, 0, 8, CardHeight));
        glyph.RenderAction = s => s.DrawRectangle(Brushes.Lime, new Rect(0, 0, 4, 4));
        scene.Over[1].Add(glyph);

        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        RenderDirty.MarkStructural();
        scene.Draw();
        var shown = Pixels(scene.Renderer);

        scene.Over[1].Visibility = Visibility.Hidden;
        scene.Draw();

        var hidden = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "hiding it must patch the recorded frame, not re-walk the scene");
        Assert.That(DifferingPixels(shown, hidden), Is.Not.Zero, "...and it must actually stop drawing");
        AssertMatchesAFullWalk(scene, hidden, "what the patch leaves must be what a walk draws");

        scene.Over[1].Visibility = Visibility.Visible;
        scene.Draw();
        Assert.That(DifferingPixels(shown, Pixels(scene.Renderer)), Is.Zero, "showing it again must put back exactly what it drew");
    }

    // Hiding is INHERITED, and a WALK has to honour that as much as a patch does. The walk stops at nothing now - it goes
    // through a hidden element to keep its subtree's ranks - so it has to carry "hidden" down itself. It did not, and the
    // hidden element's visible children went on drawing: every tab wore its close-button glyph until the pointer touched
    // one, which is the state a fresh window opens in.
    [Test]
    public void AChildOfAHiddenParent_IsGoneFromAWALKedFrameToo()
    {
        using var scene = NewScene();

        var glyph = Placed(new Rect(0, 0, 8, CardHeight));
        glyph.RenderAction = s => s.DrawRectangle(Brushes.Lime, new Rect(0, 0, 4, 4));
        scene.Over[1].Add(glyph);
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 6, CardHeight));
        scene.Over[1].Invalidate();
        RenderDirty.MarkStructural();
        scene.Draw();
        Assert.That(CountLime(Pixels(scene.Renderer)), Is.Not.Zero, "the child has to be drawing in the first place");

        scene.Over[1].Visibility = Visibility.Hidden;
        RenderDirty.MarkStructural();   // a WALK, not the patch - that is the path this pins
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.False, "this test is about the walk");
        Assert.That(CountLime(Pixels(scene.Renderer)), Is.Zero, "a hidden parent must take what its children draw with it");
    }

    // A control whose fill is arbitrary GEOMETRY - a Path, which is what a close button's glyph is - must be spliced in
    // like any other batched family. It is not: that fill lives in InstancedFillCollector, which is not a BatchArena at
    // all (per-KEY storage with its own ring, four parallel instance families, and a flush record on top), so the splice
    // has nothing to name and the frame walks. Measured live: hovering one close button costs 389 walks in eight seconds
    // of flipping, every one of them `notOneArena<Path>` / `noArena<Path>`.
    // RED ON PURPOSE - it is the specification for making that collector an arena.
    [Test]
    public void AVectorFillThatStartsDrawing_IsSplicedInLikeARectangle()
    {
        FrameTrace.Enabled = true;   // so a refusal names itself in the message below
        using var scene = NewScene();

        scene.Far[1].RenderAction = s => s.DrawGeometry(Brushes.Green, new RectangleGeometry(new Rect(4, 0, 20, CardHeight)));
        scene.Far[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            $"a vector fill must splice like a rectangle (refused by {FrameTrace.Refuser})");
        AssertMatchesAFullWalk(scene, patched, "and what it splices in must be what a walk draws");
    }

    // Hide and SHOW again, with a vector fill - a close button's glyph is a Path, and showing one is the case a hover
    // makes dozens of times a minute. The patch must put back exactly what it took away; a splice that succeeds and draws
    // nothing is worse than one that refuses, because the refusal at least walks and gets it right.
    [Test]
    public void AVectorFillHiddenAndShownAgain_IsPutBackExactly()
    {
        FrameTrace.Enabled = true;
        using var scene = NewScene();

        scene.Over[1].RenderAction = s => s.DrawGeometry(Brushes.Lime, new RectangleGeometry(new Rect(0, 0, 8, 8)));
        scene.Over[1].Invalidate();
        RenderDirty.MarkStructural();
        scene.Draw();
        var shown = Pixels(scene.Renderer);
        Assert.That(CountLime(shown), Is.Not.Zero, "it has to be drawing in the first place");

        scene.Over[1].Visibility = Visibility.Hidden;
        scene.Draw();
        Assert.That(CountLime(Pixels(scene.Renderer)), Is.Zero, $"hiding must take it off the frame (by {FrameTrace.Refuser})");

        scene.Over[1].Visibility = Visibility.Visible;
        scene.Draw();
        Assert.That(DifferingPixels(shown, Pixels(scene.Renderer)), Is.Zero,
            $"showing it again must put back exactly what it drew (by {FrameTrace.Refuser})");
    }

    private static int CountLime(byte[] pixels)
    {
        var n = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] < 200 && pixels[i + 1] > 200 && pixels[i + 2] < 200) n++;   // BGRA
        }

        return n;
    }

    // The unit of repair is the SEGMENT, and every batched family is drawn from one - so which family a control happens
    // to draw with has no business deciding whether its frame can be patched. It did: the splice baked rectangles and
    // refused everything else, so an ellipse appearing beside one cost a walk of the whole window. Measured on a live
    // scene, the same refusal for a text block was 25 walks at ~25 ms apiece.
    [Test]
    public void AnEllipseThatStartsDrawing_IsSplicedInLikeARectangle()
    {
        using var scene = NewScene();

        scene.Far[1].RenderAction = s => s.DrawEllipse(new Rect(4, 0, 20, CardHeight), Brushes.Green, 0, 360, default(EllipseType));
        scene.Far[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            $"an ellipse must splice like a rectangle (refused by {FrameTrace.Refuser})");
        AssertMatchesAFullWalk(scene, patched, "and what it splices in must be what a walk draws");
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout)
            => throw new NotSupportedException();
        public Adamantium.Graphics.Fonts.FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
