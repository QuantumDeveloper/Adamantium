using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// GPU-free tests for the RenderCache lifecycle and update dispatch. Real controls,
// fake render-unit factory. These are kept as permanent regression coverage.
[TestFixture]
public class RenderCacheTests
{
    private TestRoot _root;
    private FakeRenderUnitFactory _factory;
    private RenderCache _cache;

    private static readonly Rect Box = new Rect(0, 0, 10, 10);

    [SetUp]
    public void SetUp()
    {
        _root = new TestRoot();
        _factory = new FakeRenderUnitFactory();
        _cache = new RenderCache(new DrawingContext(), _factory);
    }

    private void RenderFrame() => _cache.BuildFromVisualTree(_root);

    private static void DrawsRectangle(TestControl c, Brush brush = null) =>
        c.RenderAction = s => s.DrawRectangle(brush ?? Brushes.Red, Box);

    private TestControl AddControl()
    {
        var c = new TestControl();
        _root.Add(c);
        return c;
    }

    // -------- cache correctness --------

    [Test]
    public void Hidden_Control_DoesNotAbortTraversal()
    {
        var a = AddControl(); DrawsRectangle(a);
        var b = AddControl(); DrawsRectangle(b); b.Visibility = Visibility.Hidden;
        var c = AddControl(); DrawsRectangle(c);

        RenderFrame();

        // A and C must have produced units; B (hidden) is skipped. Pre-fix, B's hidden state aborted the
        // whole walk and C was never reached.
        Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(a));
        Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(c));
        Assert.That(_factory.Created.Select(u => u.Component), Does.Not.Contain(b));
    }

    [Test]
    public void GrowingCommandCount_DoesNotThrow_AndCreatesUnit()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        Assert.That(_factory.Created.Count, Is.EqualTo(1));

        // Frame 2: same control now emits TWO commands. Pre-fix off-by-one indexed units[1] out of range.
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box).DrawRectangle(Brushes.Blue, Box);
        c.Invalidate();
        Assert.DoesNotThrow(RenderFrame);
        Assert.That(_factory.Created.Count, Is.EqualTo(2));
    }

    [Test]
    public void DirtyControl_RenderingNothing_ClearsItsUnits()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Frame 2: control is dirty and now draws nothing -> its stale unit must be released.
        c.RenderAction = null;
        c.Invalidate();
        RenderFrame();

        Assert.That(unit.DeferDisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void CleanControl_WithNoCommands_ReusesCachedUnits()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Frame 2: NOT invalidated -> Render() is a no-op (no commands) -> cached unit must be reused, not freed.
        RenderFrame();

        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));
        Assert.That(_factory.Created.Count, Is.EqualTo(1));

        _cache.Render();
        Assert.That(unit.RenderCount, Is.GreaterThan(0));
    }

    // -------- resource lifetime --------

    [Test]
    public void Detach_FromTree_DefersDisposalOfUnits()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        _root.Remove(c); // detaches the control from the visual tree

        // Disposal must NOT be triggered by the detach event itself: that event fires during layout/Update,
        // which is the wrong phase for the frame-slot defer queue (BeginDraw would drain it the same frame,
        // disposing a unit still referenced by _renderUnits and still in GPU flight). Disposal happens on
        // the next build, via ReconcileDetachedControls.
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));

        RenderFrame(); // build -> reconcile finds the detached control -> defers its units exactly once
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void Hidden_Control_RetainsResources_AndIsNotRecreatedOnShow()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Hide: skipped from drawing but still attached -> resources kept.
        c.Visibility = Visibility.Collapsed;
        RenderFrame();
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));

        // Show again: must reuse the same unit, not allocate a new one.
        c.Visibility = Visibility.Visible;
        RenderFrame();
        Assert.That(_factory.Created.Count, Is.EqualTo(1));
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));
    }

    [Test]
    public void PayloadTypeMismatch_ReplacesUnit_DeferringTheOld()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        var rectUnit = _factory.Created.Single();
        Assert.That(rectUnit.PayloadType.Name, Is.EqualTo("RectanglePayload"));

        // Same slot, different payload type -> Match fails -> old deferred, new created.
        c.RenderAction = s => s.DrawEllipse(Box, Brushes.Red, 0, 360, default(EllipseType));
        c.Invalidate();
        RenderFrame();

        Assert.That(rectUnit.DeferDisposeCount, Is.EqualTo(1));
        Assert.That(_factory.Created.Count, Is.EqualTo(2));
        Assert.That(_factory.Created[1].PayloadType.Name, Is.EqualTo("EllipsePayload"));
    }

    // -------- update dispatch (matching unit is updated, not recreated) --------

    // Repro for "nothing renders inside panels" on a gallery tab. A ContentPresenter attached to the root renders a
    // data item through a DataTemplate whose root is a ContentControl (a <View>) holding a StackPanel > Grid > Border -
    // exactly the tab-body shape, built DYNAMICALLY into an already-attached tree (unlike the old directly-nested views).
    // The deep Border must (a) attach to the tree and (b) produce a render unit that ISN'T freed as detached.
    [Test]
    public void TabBodyStyle_DynamicPanelChild_AttachesAndProducesUnit()
    {
        Border border = null;
        var template = new DataTemplate(() =>
        {
            border = new Border { Width = 56, Height = 34, Background = Brushes.Red };
            var grid = new Grid { Width = 300, Height = 60 };
            ((IContainer)grid).AddOrSetChildComponent(border);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            ((IContainer)stack).AddOrSetChildComponent(grid);
            var view = new ContentControl { Content = stack };
            return new TemplateResult { RootComponent = view };
        });

        var cp = new ContentPresenter { Content = new object(), ContentTemplate = template };
        _root.Add(cp);
        // Measure/arrange runs the ContentPresenter's BuildCurrent -> builds + attaches the template content.
        cp.Measure(new Size(400, 400));
        cp.Arrange(new Rect(0, 0, 400, 400));

        Assert.That(border, Is.Not.Null, "the template built its Border");
        Assert.That(border.IsAttachedToVisualTree, Is.True, "the panel's Border attached to the visual tree");
        // A unit is created even for a ZERO-size rect, so the real question is whether the dynamically-built content got
        // ARRANGED (RenderSize is set by Arrange). If arrange doesn't reach content built during the measure pass, the
        // Border stays 0x0 and draws nothing - while TextBlocks still show via their own raster path.
        Assert.That(border.RenderSize, Is.EqualTo(new Size(56, 34)), "the Border was ARRANGED to its own size (not left 0x0)");

        RenderFrame();
        Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(border),
            "the Border produced a render unit (so it actually draws) and isn't skipped/freed as detached");
    }

    // Regression: an ARRANGE that changes the size must re-run OnRender. A control first laid out at a stale size (e.g.
    // 0x0 while still unarranged - content built during a measure pass, like a tab body) cached that geometry as valid;
    // MeasureCore invalidates render geometry but ArrangeCore did NOT, so a later size-only arrange left OnRender drawing
    // the old 0x0 rect forever (invisible panels). Re-arrange to a new size WITHOUT re-measuring (measure would mask it).
    [Test]
    public void ArrangeToNewSize_ReRunsOnRender_NotStaleGeometry()
    {
        var b = new Border
        {
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _root.Add(b);

        b.Measure(new Size(100, 100));
        b.Arrange(new Rect(0, 0, 20, 20));
        RenderFrame();
        var unit = _factory.Created.Single();          // the Border's fill unit
        var updatesBefore = unit.UpdateWithCommandCount;

        b.Arrange(new Rect(0, 0, 60, 40));             // size-only re-arrange (no re-measure)
        RenderFrame();

        Assert.That(unit.UpdateWithCommandCount, Is.GreaterThan(updatesBefore),
            "an arrange-only size change must re-run OnRender (re-record the fill at the new size), not keep stale geometry");
    }

    [Test]
    public void SamePayloadType_UpdatesExistingUnit_NotRecreated()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        var unit = _factory.Created.Single();

        // Re-render with a different brush (same payload type) -> the cache must reuse + UpdateWithDrawCommand.
        c.RenderAction = s => s.DrawRectangle(Brushes.Blue, Box);
        c.Invalidate();
        RenderFrame();

        Assert.That(_factory.Created.Count, Is.EqualTo(1));
        Assert.That(unit.UpdateWithCommandCount, Is.GreaterThan(0));
        Assert.That(unit.DeferDisposeCount, Is.EqualTo(0));
    }

    // -------- dirty-driven build: clean skip / partial in-place re-render / full walk on structural change (§4a/§4i) --------

    [Test]
    public void CleanFrame_IsSkipped()
    {
        var c = AddControl(); DrawsRectangle(c);

        RenderFrame();
        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Full), "the first build is a full walk");

        RenderFrame();
        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Clean), "a frame with no scene change does no work");
    }

    // The point of dirty regions: a same-shape change re-renders ONLY the dirty control, not its unchanged siblings, and
    // does NOT do a full tree walk.
    [Test]
    public void ColourChange_IsPartial_ReRendersOnlyTheDirtyControl()
    {
        var a = AddControl(); DrawsRectangle(a);
        var b = AddControl(); DrawsRectangle(b);
        RenderFrame();
        var aBefore = a.OnRenderCount;
        var bBefore = b.OnRenderCount;

        a.RenderAction = s => s.DrawRectangle(Brushes.Blue, Box);   // same shape, new colour
        a.Invalidate();
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Partial), "a same-shape change is a partial rebuild, not a full walk");
            Assert.That(a.OnRenderCount, Is.GreaterThan(aBefore), "the dirty control re-rendered");
            Assert.That(b.OnRenderCount, Is.EqualTo(bBefore), "an UNCHANGED control must NOT re-render");
        });
    }

    [Test]
    public void Move_IsPartial()
    {
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        RenderFrame();
        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Clean));

        c.Bounds = new Rect(5, 5, 10, 10);   // a move: re-bake transforms, no re-record, no full walk
        RenderFrame();
        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Partial), "a move is handled without a full tree walk");
    }

    [Test]
    public void StructuralChange_IsSpliced_NotAFullWalk()
    {
        var a = AddControl(); DrawsRectangle(a);
        RenderFrame();
        RenderFrame();
        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Clean));

        var aRenders = a.OnRenderCount;
        var b = AddControl(); DrawsRectangle(b);   // adding content changes the paint-order list
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural), "added content is SPLICED into the paint order");
            Assert.That(a.OnRenderCount, Is.EqualTo(aRenders), "the existing content must NOT be re-recorded - that is the whole point");
            Assert.That(_factory.Created.Select(u => u.Component), Does.Contain(b), "the new control's unit was built");
        });
        AssertPaintOrderMatchesFullWalk("appending a sibling");
    }

    // -------- the structural splice: the paint order it maintains must be EXACTLY the one a full walk derives --------
    //
    // Drawing in the wrong order is drawing the wrong picture, so every case is held against the same reference: build the
    // SAME tree in a fresh cache (whose first build is always a full walk) and compare. That is the invariant; the rest -
    // which frame kind ran, how many units were created - is just how it was achieved.

    private void AssertPaintOrderMatchesFullWalk(string because)
    {
        var reference = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());
        _root.InvalidateRender(true);   // a fresh cache records only what is DIRTY - make the whole tree record again
        reference.BuildFromVisualTree(_root);
        Assert.That(_cache.PaintOrder, Is.EqualTo(reference.PaintOrder).AsCollection, because);
    }

    [Test]
    public void Splice_InsertInTheMiddle_KeepsPaintOrder()
    {
        var a = AddControl(); DrawsRectangle(a);
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();

        var b = new TestControl(); DrawsRectangle(b);
        _root.Insert(1, b);   // between a and c - the case dense ranks cannot express without renumbering
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural));
        AssertPaintOrderMatchesFullWalk("inserting between two existing siblings");
    }

    [Test]
    public void Splice_InsertAtTheFront_KeepsPaintOrder()
    {
        var b = AddControl(); DrawsRectangle(b);
        RenderFrame();

        var a = new TestControl(); DrawsRectangle(a);
        _root.Insert(0, a);   // before everything: its rank must land between the ROOT's and b's
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural));
        AssertPaintOrderMatchesFullWalk("inserting before the first sibling");
    }

    [Test]
    public void Splice_AddsWholeSubtree_InPaintOrder()
    {
        var a = AddControl(); DrawsRectangle(a);
        RenderFrame();

        // A realized container: a subtree assembled BEFORE it is attached (exactly how a list container arrives).
        var container = new TestControl(); DrawsRectangle(container, Brushes.Green);
        var child1 = new TestControl(); DrawsRectangle(child1, Brushes.Blue);
        var child2 = new TestControl(); DrawsRectangle(child2, Brushes.Yellow);
        container.Add(child1);
        container.Add(child2);
        _root.Add(container);
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural));
        AssertPaintOrderMatchesFullWalk("a whole subtree spliced in at once");
    }

    [Test]
    public void Splice_Removal_FreesUnits_AndKeepsPaintOrder()
    {
        var a = AddControl(); DrawsRectangle(a);
        var b = AddControl(); DrawsRectangle(b);
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();
        var bUnit = _factory.Created.First(u => u.Component == b);

        _root.Remove(b);
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural));
            Assert.That(bUnit.DeferDisposeCount, Is.GreaterThan(0), "the removed control's units were freed");
            Assert.That(_cache.PaintOrder, Does.Not.Contain(b.RenderId));
        });
        AssertPaintOrderMatchesFullWalk("after removing a middle sibling");
    }

    [Test]
    public void Splice_Recycled_Container_MovedToTheEnd_KeepsPaintOrder()
    {
        var a = AddControl(); DrawsRectangle(a);
        var b = AddControl(); DrawsRectangle(b);
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();

        // What a virtualizing panel does to a container that scrolled off one end and back on at the other: same object,
        // new position. Its units must survive AND its group must move in the paint order.
        _root.Remove(a);
        _root.Add(a);
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural));
        AssertPaintOrderMatchesFullWalk("a recycled container re-added at the end");
    }

    [Test]
    public void Splice_HiddenThenShown_KeepsPaintOrder()
    {
        var a = AddControl(); DrawsRectangle(a);
        var b = AddControl(); DrawsRectangle(b);
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();

        b.Visibility = Visibility.Collapsed;   // leaves the drawn set
        RenderFrame();
        Assert.That(_cache.PaintOrder, Does.Not.Contain(b.RenderId), "a hidden control draws nothing");

        b.Visibility = Visibility.Visible;     // ...and comes back, at its old place
        b.Invalidate();
        RenderFrame();
        AssertPaintOrderMatchesFullWalk("a control hidden and shown again");
    }

    // Hiding a PARENT takes its whole subtree out of the drawn set, even though the children are still Visible and still
    // attached - they simply have a hidden ancestor. This is how the virtualizer recycles a tile (it collapses the container,
    // not the content), so the splice must free the content's units too, or they keep drawing at their old slots: the gaps and
    // overlapping tiles that appear while the size slider is dragged.
    [Test]
    public void Splice_CollapsedParent_FreesItsWholeSubtree()
    {
        var host = AddControl(); DrawsRectangle(host);
        var content = new TestControl(); DrawsRectangle(content, Brushes.Blue);
        host.Add(content);
        RenderFrame();
        Assert.That(_cache.PaintOrder, Does.Contain(content.RenderId), "the content draws while its host is visible");

        host.Visibility = Visibility.Collapsed;   // the container is recycled - only the HOST is named by the mark
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(_cache.PaintOrder, Does.Not.Contain(host.RenderId), "the hidden host stops drawing");
            Assert.That(_cache.PaintOrder, Does.Not.Contain(content.RenderId),
                "...and so does everything under it - a still-Visible child of a hidden parent is NOT drawn");
        });
        AssertPaintOrderMatchesFullWalk("after collapsing a parent");
    }

    // A component can change SIZE without its recorded CONTENT going stale: RenderSize marks it dirty, but leaves
    // IsGeometryValid true (it draws the same thing, just at a new size), so the record rightly skips re-recording it. Its
    // frozen layout must still be re-frozen - the draw pass reads the picture's geometry from there, so a stale entry paints
    // the component at its previous size. On a grid of tiles that is exactly what the size slider produces: gaps when the
    // cells grow, overlaps when they shrink.
    [Test]
    public void ResizedButNotReRecorded_Component_IsDrawnAtItsNewSize()
    {
        var c = AddControl(); DrawsRectangle(c);
        c.RenderSize = new Size(50, 50);
        RenderFrame();
        Assert.That(_cache.AppliedSnapshot[c].RenderSize, Is.EqualTo(new Size(50, 50)));

        // The cell grew: the tile is arranged bigger, but its CONTENT is unchanged - so it is never re-rendered.
        var rendersBefore = c.OnRenderCount;
        c.RenderSize = new Size(90, 90);
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(c.OnRenderCount, Is.EqualTo(rendersBefore), "a pure resize does not re-record the content");
            Assert.That(_cache.AppliedSnapshot[c].RenderSize, Is.EqualTo(new Size(90, 90)),
                "...but the draw side MUST see the new size - otherwise it paints the tile at its old one");
        });
    }

    // A brush MUTATED INTERNALLY (an animation moving its Opacity, a theme fade, a pulsing placeholder) changes nothing about
    // what an element draws: same commands, same kinds, same geometry - and the recorded command holds THAT SAME brush by
    // reference, so the GPU data is baked from it anyway. So the element must NOT be re-rendered; the renderer only re-bakes
    // the units it already has.
    //
    // This is what makes an animated SHARED brush affordable. It is routinely shared by thousands of elements (a keyed theme
    // brush - every loading skeleton paints with ONE pulsing brush), so its change reaches all of them on every tick. Treating
    // that as geometry re-ran OnRender for each: measured at ~470 cards per frame, half of a tile fill's throughput, for one
    // number.
    [Test]
    public void BrushMutation_RepaintsWithoutReRecordingTheElement()
    {
        // ONE brush, painting several elements - exactly how a keyed theme brush (and the skeleton pulse) is used.
        var brush = new SolidColorBrush(Colors.Red);
        var a = new Border { Width = 20, Height = 20, Background = brush };
        var b = new Border { Width = 20, Height = 20, Background = brush };
        _root.Add(a);
        _root.Add(b);
        a.Arrange(new Rect(0, 0, 20, 20));
        b.Arrange(new Rect(0, 20, 20, 20));
        RenderFrame();

        var units = _factory.Created.Count;
        Assert.That(units, Is.GreaterThan(0), "the borders drew something to begin with");
        foreach (var unit in _factory.Created) Assume.That(unit.UpdateWithCommandCount, Is.EqualTo(0));

        brush.Opacity = 0.5;   // what the pulse animation does, on every tick
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Partial),
                "a repaint is a partial frame - the paint order is untouched");
            Assert.That(_factory.Created.Count, Is.EqualTo(units), "a repaint must not build a single new unit");
            Assert.That(_factory.Created.Sum(u => u.UpdateWithCommandCount), Is.Zero,
                "...and must not re-record the elements either: no element was asked for its draw commands again");
        });
    }

    // Clearing a component's children must DETACH them, not merely forget them. A child left pointing at its old parent while
    // absent from that parent's children is unreachable by any downward walk - yet it still reports itself attached and visible,
    // so it stays dirty forever: the splice refuses every frame (it cannot place what it cannot find) and the fallback full walk
    // does not draw it either. A templated control dropping its old template root does exactly this.
    [Test]
    public void ClearedChildren_AreDetached_NotOrphaned()
    {
        var host = AddControl(); DrawsRectangle(host);
        var old = new TestControl(); DrawsRectangle(old, Brushes.Blue);
        host.Add(old);
        RenderFrame();
        Assert.That(_cache.PaintOrder, Does.Contain(old.RenderId));

        host.ClearChildren();   // what a templated control does when it drops its template
        var replacement = new TestControl(); DrawsRectangle(replacement, Brushes.Green);
        host.Add(replacement);

        Assert.Multiple(() =>
        {
            Assert.That(old.VisualParent, Is.Null, "a cleared child must not keep pointing at its old parent");
            Assert.That(old.IsAttachedToVisualTree, Is.False, "...and it must not still call itself attached");
        });

        old.Invalidate();   // the orphan, still dirty, used to force a whole-tree re-record on EVERY frame
        RenderFrame();

        Assert.Multiple(() =>
        {
            Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural),
                "a dirty component that left the tree must not drag the frame into a full walk");
            Assert.That(_cache.PaintOrder, Does.Not.Contain(old.RenderId));
        });
        AssertPaintOrderMatchesFullWalk("after the children were cleared and replaced");
    }

    // Hiding a control in the SAME frame that runs out of rank space. The renumber hands fresh ranks to what is DRAWN, so the
    // control being hidden gets none - and if "did it leave the paint order" is answered by asking for a rank, it is then
    // silently skipped: the applier is never told to drop it, and it keeps painting, frozen at its last slot on top of real
    // content. (That is the stuck skeleton card: a recycled placeholder that never went away.)
    [Test]
    public void Splice_HiddenDuringARenumber_StopsPainting()
    {
        var a = AddControl(); DrawsRectangle(a);
        var victim = AddControl(); DrawsRectangle(victim, Brushes.Blue);
        var z = AddControl(); DrawsRectangle(z);
        RenderFrame();

        // Burn the rank gap between a and its follower, so the next insert forces a renumber.
        for (var i = 0; i < 40; i++)
        {
            var filler = new TestControl(); DrawsRectangle(filler);
            _root.Insert(1, filler);
            RenderFrame();
        }

        // ONE frame: another insert (which exhausts the gap -> renumber) AND the victim leaves the drawn set.
        var last = new TestControl(); DrawsRectangle(last);
        _root.Insert(1, last);
        victim.Visibility = Visibility.Collapsed;
        RenderFrame();

        Assert.That(_cache.PaintOrder, Does.Not.Contain(victim.RenderId),
            "a control hidden while the ranks were being renumbered must still leave the paint order");
        AssertPaintOrderMatchesFullWalk("hidden during a renumber");
    }

    // Content can enter the tree through paths that never call AddVisualChild - a Decorator (a Border) putting its Child in
    // goes straight to the visual-children collection. Such a component used to appear with nobody naming it, so the splice
    // had nowhere to place it and every frame of a list fill fell back to re-recording the whole tree. The COLLECTION names
    // them now, so no path can be forgotten.
    [Test]
    public void Splice_ContentAddedThroughDecoratorChild_IsNamed_AndSplices()
    {
        var host = AddControl(); DrawsRectangle(host);
        RenderFrame();
        RenderFrame();
        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Clean));

        // A Border's Child never goes through AddVisualChild - it is set straight on the visual-children collection.
        var border = new Border { Width = 20, Height = 20, Background = Brushes.Blue };
        var inner = new Border { Width = 10, Height = 10, Background = Brushes.Green };
        border.Child = inner;
        _root.Add(border);
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural),
            "content that arrived through Decorator.Child must still be placeable - no full walk");
        AssertPaintOrderMatchesFullWalk("a Border whose Child was set, not AddVisualChild'd");
    }

    // A virtualizing panel parks THOUSANDS of collapsed containers in a row - that is what recycling is. Placing new content
    // after them means stepping back over every one of them to find the last thing that actually paints, and doing that by
    // recursion overflowed the stack and took the whole app down.
    [Test]
    public void Splice_ThousandsOfHiddenSiblings_DoesNotOverflow()
    {
        var anchor = AddControl(); DrawsRectangle(anchor);
        for (var i = 0; i < 5000; i++)
        {
            var hidden = AddControl();
            DrawsRectangle(hidden);
            hidden.Visibility = Visibility.Collapsed;
        }
        RenderFrame();

        var tail = new TestControl(); DrawsRectangle(tail, Brushes.Blue);
        _root.Add(tail);   // its rank must be interpolated past 5000 collapsed siblings
        Assert.DoesNotThrow(RenderFrame);

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural));
        AssertPaintOrderMatchesFullWalk("new content after thousands of recycled (hidden) containers");
    }

    // Several runs of new content under ONE parent, separated by hidden (recycled) siblings - what a virtualizing panel looks
    // like mid-scroll. Ranking the second run means looking back past the hidden one, straight into a tile the FIRST run was
    // just given a rank: the plan has to see itself, or it refuses to place a tile against its own work and the frame falls
    // back to re-recording the entire tree (the biggest remaining source of full walks on a 4K fill).
    [Test]
    public void Splice_SeveralNewRunsSeparatedByHiddenSiblings_StillSplices()
    {
        var a = AddControl(); DrawsRectangle(a);
        var hidden1 = AddControl(); DrawsRectangle(hidden1);
        var b = AddControl(); DrawsRectangle(b);
        var hidden2 = AddControl(); DrawsRectangle(hidden2);
        var c = AddControl(); DrawsRectangle(c);
        RenderFrame();

        hidden1.Visibility = Visibility.Collapsed;
        hidden2.Visibility = Visibility.Collapsed;
        RenderFrame();

        // Two separate runs of brand-new tiles, each landing next to a hidden sibling.
        var new1 = new TestControl(); DrawsRectangle(new1, Brushes.Blue);
        var new2 = new TestControl(); DrawsRectangle(new2, Brushes.Green);
        _root.Insert(1, new1);   // between a and hidden1
        _root.Insert(4, new2);   // between b and hidden2
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural),
            "new content on both sides of hidden siblings must still splice - not fall back to a full walk");
        AssertPaintOrderMatchesFullWalk("two new runs separated by hidden siblings");
    }

    // The rank space between two neighbours is finite: every insert into the SAME gap halves it, so a panel realizing tiles
    // into one spot eventually runs out. That must cost a RENUMBER - an order-only walk plus a re-sort - and never a re-record
    // of the whole tree: the numbers changed, not the content. (It used to fall back to a full walk, which on a 4K fill is
    // 100-200 ms of re-recording ~20 000 components to achieve some fresh integers.)
    [Test]
    public void Splice_ExhaustedRankGap_Renumbers_WithoutReRecordingTheTree()
    {
        var a = AddControl(); DrawsRectangle(a);
        var z = AddControl(); DrawsRectangle(z);
        RenderFrame();

        for (var i = 0; i < 60; i++)
        {
            var mid = new TestControl(); DrawsRectangle(mid);
            _root.Insert(1, mid);   // always into the same shrinking gap between a and its follower

            // Measured across the FRAME only: the paint-order check below builds a reference cache, and to do that it has to
            // dirty the whole tree - which would otherwise look like the engine re-rendering it.
            var rendersBefore = a.OnRenderCount + z.OnRenderCount;
            RenderFrame();

            Assert.Multiple(() =>
            {
                Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Structural),
                    $"insert #{i}: an exhausted rank gap must renumber, not re-record the tree");
                Assert.That(a.OnRenderCount + z.OnRenderCount, Is.EqualTo(rendersBefore),
                    $"insert #{i}: the untouched controls were asked to re-render - renumbering moves numbers, not content");
            });
        }

        AssertPaintOrderMatchesFullWalk("after 60 inserts into an ever-tighter gap (with renumbers along the way)");
    }

    // A change that alters a control's draw-command COUNT adds units - which the partial pass used to hand to a full walk.
    // It no longer needs to: the count change stays LOCAL to that control's group (BuildUnitsFor refreshes its unit list in
    // place; no other group moves), so it is applied as a PARTIAL. What matters is that the new content is not lost.
    [Test]
    public void CommandCountChange_IsAppliedInPlace()
    {
        var c = AddControl();
        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box);
        RenderFrame();
        Assert.That(_factory.Created.Count, Is.EqualTo(1));

        c.RenderAction = s => s.DrawRectangle(Brushes.Red, Box).DrawRectangle(Brushes.Blue, Box);   // 1 -> 2 commands
        c.Invalidate();
        RenderFrame();

        Assert.That(_cache.LastBuildKind, Is.EqualTo(RenderBuildKind.Partial), "a count change is spliced into that one group - no tree walk");
        Assert.That(_factory.Created.Count, Is.EqualTo(2), "the control's new second unit was created");
    }
}
