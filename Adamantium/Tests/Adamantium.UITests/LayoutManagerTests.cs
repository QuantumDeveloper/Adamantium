using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Phase 1 of the layout-manager plan: layout is dirty-queue-driven, not a per-frame full-tree walk. These tests assert
// the headline guarantee - a CLEAN frame (nothing invalidated) triggers ZERO Measure/Arrange calls - and that
// invalidating a node DOES schedule work on the next pass, then settles back to zero.
public class LayoutManagerTests
{
    // A control that re-invalidates its own measure DURING arrange, exactly once - the re-entrancy the manager must
    // survive (the old VirtualizingPanel._inLayout crutch muted this; now the manager's drain loop must converge).
    private sealed class ReentrantArrangeBorder : Border
    {
        private bool _done;

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            if (!_done)
            {
                _done = true;
                InvalidateMeasure();   // re-enter the layout system mid-arrange
            }
            return size;
        }
    }

    // A Border that counts how many times its MeasureOverride runs - to prove finer measure propagation (a child
    // re-measure that doesn't change the child's size must not re-run the parent's measure).
    private sealed class MeasureCountingBorder : Border
    {
        public int MeasureOverrideCount;

        protected override Size MeasureOverride(Size availableSize)
        {
            MeasureOverrideCount++;
            return base.MeasureOverride(availableSize);
        }
    }

    // A visual root with a client viewport (for the visible-first test), mirroring a window's role.
    private sealed class TestWindowRoot : Grid, Adamantium.UI.Core.IRootVisualComponent
    {
        public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
        public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
        public void AttachContextAndInitialize(Adamantium.UI.Core.IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public Adamantium.UI.Core.IUIContext UIContext => null;
    }

    private static (Border root, Border leaf) BuildTree()
    {
        var leaf = new Border { Width = 50, Height = 50 };
        var stack = new StackPanel();
        stack.Children.Add(leaf);
        stack.Children.Add(new Border { Width = 50, Height = 30 });
        var root = new Border { Width = 200, Height = 200, Child = stack };
        return (root, leaf);
    }

    // The settle signal a multi-pass cascade (a theme swap) reports completion on. It must fire ONLY on a pass that found
    // nothing left to do - NOT on a pass that merely drained its queues, because mid-cascade every pass looks like that
    // (re-styling re-templates, which re-measures, which re-arranges...). Getting this wrong would announce "theme
    // applied" while the tree is still rebuilding - exactly the moment a busy indicator must NOT be taken down.
    [Test]
    public void Quiescent_FiresOnlyOnAPassThatFoundNothingToDo()
    {
        var leaf = new Border { Width = 50, Height = 50 };
        var root = new TestWindowRoot { Width = 200, Height = 200, ClientWidth = 200, ClientHeight = 200 };
        root.Children.Add(leaf);

        var settles = 0;
        void OnQuiescent(LayoutManager manager) => settles++;
        LayoutManager.Quiescent += OnQuiescent;
        try
        {
            WindowExtension.UpdateTree(root);   // lays the tree out: this pass DID work
            Assert.Multiple(() =>
            {
                Assert.That(settles, Is.EqualTo(0), "a pass that did work is not a settle");
                Assert.That(LayoutManager.For(root).IsSettled, Is.True, "...though it left nothing queued");
            });

            WindowExtension.UpdateTree(root);   // nothing dirty -> workless pass
            Assert.That(settles, Is.EqualTo(1), "a workless pass IS the settle");

            leaf.Width = 80;
            Assert.That(LayoutManager.For(root).IsSettled, Is.False, "an invalidated tree owes layout work");

            WindowExtension.UpdateTree(root);   // re-lays it out: work again
            Assert.That(settles, Is.EqualTo(1), "still not settled - the cascade was doing work");

            WindowExtension.UpdateTree(root);
            Assert.That(settles, Is.EqualTo(2), "settled once the work ran out");
        }
        finally
        {
            LayoutManager.Quiescent -= OnQuiescent;
        }
    }

    [Test]
    public void CleanFrame_TriggersNoMeasureOrArrange()
    {
        var (root, _) = BuildTree();

        // First pass lays the tree out (measure + arrange happen).
        WindowExtension.UpdateTree(root);

        var measureBefore = MeasurableUIComponent.TotalMeasureCalls;
        var arrangeBefore = MeasurableUIComponent.TotalArrangeCalls;

        // Second pass with NOTHING invalidated: the dirty queues are empty, so the manager must touch nothing. The old
        // full-tree walk would have called Measure/Arrange on every node here.
        WindowExtension.UpdateTree(root);

        Assert.Multiple(() =>
        {
            Assert.That(MeasurableUIComponent.TotalMeasureCalls - measureBefore, Is.EqualTo(0), "a clean frame measured something");
            Assert.That(MeasurableUIComponent.TotalArrangeCalls - arrangeBefore, Is.EqualTo(0), "a clean frame arranged something");
        });
    }

    [Test]
    public void InvalidatingNode_SchedulesLayoutThenSettles()
    {
        var (root, leaf) = BuildTree();
        WindowExtension.UpdateTree(root);

        // Invalidate one node (Width is AffectsMeasure -> InvalidateMeasure -> enqueues the dirty subtree's top).
        leaf.Width = 80;

        var measureBeforeDirty = MeasurableUIComponent.TotalMeasureCalls;
        WindowExtension.UpdateTree(root);
        var dirtyDelta = MeasurableUIComponent.TotalMeasureCalls - measureBeforeDirty;

        // Next pass is clean again -> back to zero work (the queue is drained and nothing re-dirtied).
        var measureBeforeClean = MeasurableUIComponent.TotalMeasureCalls;
        WindowExtension.UpdateTree(root);
        var cleanDelta = MeasurableUIComponent.TotalMeasureCalls - measureBeforeClean;

        Assert.Multiple(() =>
        {
            Assert.That(dirtyDelta, Is.GreaterThan(0), "invalidating a node should schedule measure work on the next pass");
            Assert.That(cleanDelta, Is.EqualTo(0), "after settling, a clean frame does no measure work");
            Assert.That(leaf.RenderSize.Width, Is.EqualTo(80).Within(0.5), "the invalidated node was re-laid-out to its new size");
        });
    }

    // Root-cause regression for the "recycled virtualized tiles frozen at a previous cell size" bug: when a child's
    // measure is invalidated AFTER the parent measured but BEFORE the parent arranges (exactly what a mid-pass content
    // rebind does to a container's inner ContentPresenter), the parent's arrange cascade reaches that child with an
    // invalid measure. Arrange used to ABORT there, leaving the child frozen at its old size while the parent - arranged
    // to the NEW size and now valid - never re-cascaded into it. It must instead re-measure inline and land at the
    // parent's new slot. (The forced-Measure/Arrange virtualization tests never hit this because they never leave a child
    // measure-invalid across a parent arrange.)
    [Test]
    public void Arrange_ChildMeasureInvalidatedAfterParentMeasure_LandsAtParentSlot_NotFrozen()
    {
        var child = new Border();                       // Stretch, no intrinsic size -> fills its slot
        var parent = new Border { Child = child };

        parent.Measure(new Size(100, 100));
        parent.Arrange(new Rect(0, 0, 100, 100));
        Assert.That(child.RenderSize.Width, Is.EqualTo(100).Within(0.5), "baseline: child fills the 100 slot");

        // Grow the parent, but invalidate the child's measure AFTER the parent's measure (so the arrange cascade reaches
        // a measure-invalid child) - the exact ordering a recycled container's content rebind produces mid-pass.
        parent.Measure(new Size(200, 200));
        child.InvalidateMeasure();
        parent.Arrange(new Rect(0, 0, 200, 200));

        Assert.That(child.RenderSize.Width, Is.EqualTo(200).Within(0.5),
            "child must land at the parent's NEW slot (arrange re-measures inline), not freeze at the old size");
        Assert.That(child.RenderSize.Height, Is.EqualTo(200).Within(0.5));
    }

    // Root-cause regression for "a theme swap leaves the window EMPTY until a resize".
    //
    // The dirty FLAGS live on the element, but the dirty QUEUES belong to a visual ROOT - and a DETACHED element has no
    // root, so an invalidation raised while it is detached is enqueued where no layout pass will ever look. Re-attaching
    // does not by itself undo that, and the loss is invisible whenever the re-attached subtree's own root stays
    // measure-VALID: the parent's cascade short-circuits at it (same constraint) and never reaches the dirty node below.
    //
    // That is precisely a theme swap: re-templating the window detaches its whole content subtree, everything in it is
    // re-styled (and re-templated) WHILE detached, and every invalidation that follows is dropped. A resize "fixed" it
    // only because a new constraint fails every Measure gate and brute-forces the whole tree.
    [Test]
    public void InvalidatedWhileDetached_IsRelaidOut_WhenReattached()
    {
        var leaf = new Border { Width = 50, Height = 50 };
        // Fixed size, so re-hosting it re-measures it with the SAME constraint -> its measure gate short-circuits and the
        // cascade stops HERE. Anything dirty below it is reached only if the lost invalidation was actually repaired.
        var subtree = new Border { Width = 100, Height = 100, Child = leaf };
        var host = new Border { Child = subtree };
        var root = new TestWindowRoot { Width = 200, Height = 200, ClientWidth = 200, ClientHeight = 200 };
        root.Children.Add(host);
        WindowExtension.UpdateTree(root);
        Assert.That(leaf.RenderSize.Width, Is.EqualTo(50).Within(0.5), "baseline");

        host.Child = null;                     // the template teardown detaches the whole content subtree
        Assert.That(leaf.IsAttachedToVisualTree, Is.False, "the detach must reach the whole subtree");

        leaf.Width = 80;                       // invalidated while DETACHED -> enqueued into no queue a pass will drain
        host.Child = subtree;                  // re-hosted into the new template

        WindowExtension.UpdateTree(root);

        Assert.Multiple(() =>
        {
            Assert.That(leaf.IsMeasureValid, Is.True, "a re-attached element must not stay measure-dirty forever");
            Assert.That(leaf.RenderSize.Width, Is.EqualTo(80).Within(0.5),
                "layout work raised while detached must be re-registered on re-attach, not lost");
        });
    }

    // InvalidateMeasureNextPass defers a re-measure to the NEXT pass (a virtualizing panel spreading a big realize burst
    // over frames): it must NOT re-measure in the current/settled state, must re-measure on exactly the next pass, and
    // must be one-shot (a subsequent clean pass does nothing).
    [Test]
    public void InvalidateMeasureNextPass_RemeasuresOnNextPassOnly_AndIsOneShot()
    {
        var leaf = new MeasureCountingBorder { Width = 50, Height = 50 };
        var root = new Border { Width = 200, Height = 200, Child = leaf };
        WindowExtension.UpdateTree(root);   // settle

        var before = leaf.MeasureOverrideCount;
        LayoutManager.For(leaf).InvalidateMeasureNextPass(leaf);
        Assert.That(leaf.MeasureOverrideCount, Is.EqualTo(before), "the deferral alone must not measure anything yet");

        WindowExtension.UpdateTree(root);   // promotes the deferral -> one re-measure
        Assert.That(leaf.MeasureOverrideCount, Is.EqualTo(before + 1), "the next pass promoted the deferral into a re-measure");

        var after = leaf.MeasureOverrideCount;
        WindowExtension.UpdateTree(root);   // clean pass
        Assert.That(leaf.MeasureOverrideCount, Is.EqualTo(after), "the deferral is one-shot, not sticky across passes");
    }

    // Phase 2: arrange is top-down by saved slot. Invalidating ONLY a child's arrange must re-arrange that child into
    // its own last correct slot (not park it at the parent origin), and touch only its subtree - not re-arrange the
    // whole tree from the root.
    [Test]
    public void ChildOnlyArrangeInvalidation_ReArrangesIntoCorrectSlotNotOrigin()
    {
        var a = new Border { Width = 60, Height = 50 };
        var b = new Border { Width = 60, Height = 50 };
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(a);
        stack.Children.Add(b);
        var root = new Border { Width = 200, Height = 200, Child = stack };

        WindowExtension.UpdateTree(root);
        var bSlotY = b.Bounds.Y;
        Assert.That(bSlotY, Is.GreaterThan(40), "sanity: the second vertically-stacked child should sit below the first (~y=50)");

        // Invalidate ONLY b's arrange (b's measure stays valid, its slot is unchanged).
        var arrangeBefore = MeasurableUIComponent.TotalArrangeCalls;
        b.InvalidateArrange();
        WindowExtension.UpdateTree(root);
        var arrangeDelta = MeasurableUIComponent.TotalArrangeCalls - arrangeBefore;

        Assert.Multiple(() =>
        {
            // Re-arranged into its OWN saved slot (correct position), NOT parked at the parent origin (y=0).
            Assert.That(b.Bounds.Y, Is.EqualTo(bSlotY).Within(0.5), "child re-arranged to the wrong slot (origin?) instead of its saved slot");
            // Minimal: only b (a leaf Border) re-arranged - not the whole root->stack->a->b chain.
            Assert.That(arrangeDelta, Is.EqualTo(1), "a child-only arrange invalidation should re-arrange only that child's subtree");
        });
    }

    // Finer measure propagation: invalidating a child re-measures ONLY that subtree if the child's outward size is
    // unchanged (the parent's measure doesn't depend on an internal change); a size change DOES propagate up.
    [Test]
    public void ChildRemeasure_PropagatesToParentOnlyWhenItsSizeChanges()
    {
        var child = new Border { Width = 50, Height = 50 };
        var parent = new MeasureCountingBorder { Child = child };
        var root = new Border { Width = 200, Height = 200, Child = parent };
        WindowExtension.UpdateTree(root);   // settle

        var parentMeasuresBefore = parent.MeasureOverrideCount;

        // Re-measure the child WITHOUT changing its size -> the parent must not re-measure.
        child.InvalidateMeasure();
        WindowExtension.UpdateTree(root);
        Assert.That(parent.MeasureOverrideCount, Is.EqualTo(parentMeasuresBefore),
            "a child re-measure that doesn't change its size must NOT re-measure the parent");

        // Change the child's size -> the change must propagate up and re-measure the parent.
        child.Width = 80;
        WindowExtension.UpdateTree(root);
        Assert.That(parent.MeasureOverrideCount, Is.GreaterThan(parentMeasuresBefore),
            "a child size change must propagate up and re-measure the parent");
    }

    // Phase 3: the pass must survive invalidation that happens DURING the pass. A control that invalidates its measure
    // inside ArrangeOverride must not corrupt the pass - it must converge and leave the tree fully valid + correctly
    // laid out (not exit with an unarranged node because the arrange entry was consumed before its re-measure).
    [Test]
    public void InvalidateMeasureInsideArrangeOverride_DoesNotCorruptPass()
    {
        var reentrant = new ReentrantArrangeBorder { Width = 60, Height = 40 };
        var root = new Border { Width = 200, Height = 200, Child = reentrant };

        Assert.DoesNotThrow(() => WindowExtension.UpdateTree(root), "re-entrant invalidation should not throw or spin");

        Assert.Multiple(() =>
        {
            Assert.That(reentrant.IsMeasureValid, Is.True, "re-entrant control left measure-invalid");
            Assert.That(reentrant.IsArrangeValid, Is.True, "re-entrant control left arrange-invalid (pass exited before re-arranging it)");
            Assert.That(reentrant.RenderSize.Width, Is.EqualTo(60).Within(0.5), "re-entrant control mis-sized after the pass");
        });
    }

    // A never-arranged node has empty Bounds, so the renderer draws nothing for it until it's laid out - a brand-new
    // subtree shows nothing rather than garbage.
    [Test]
    public void NeverArrangedNode_HasEmptyBounds()
    {
        var fresh = new Border { Width = 50, Height = 50 };
        Assert.Multiple(() =>
        {
            Assert.That(fresh.Bounds.Width, Is.EqualTo(0).Within(0.001), "a never-arranged node has empty bounds (drawn as nothing until laid out)");
            Assert.That(((IMeasurableComponent)fresh).PreviousArrangeSlot, Is.Null, "and no saved arrange slot yet");
        });
    }

    // Phase 4: LayoutUpdated marks "layout settled this frame" - it fires once per pass that did work, and not at all on
    // a clean frame (so a consumer can rebuild on it instead of every frame).
    [Test]
    public void LayoutUpdated_FiresOncePerSettledPass_NotOnCleanFrame()
    {
        var (root, leaf) = BuildTree();
        var manager = LayoutManager.GetOrCreate(root);
        var fired = 0;
        manager.LayoutUpdated += (_, _) => fired++;

        WindowExtension.UpdateTree(root);   // first pass does work
        Assert.That(fired, Is.EqualTo(1), "LayoutUpdated should fire once when the first pass settles");

        WindowExtension.UpdateTree(root);   // clean frame: nothing to do
        Assert.That(fired, Is.EqualTo(1), "LayoutUpdated must NOT fire on a clean frame");

        leaf.Width = 90;                    // dirty again
        WindowExtension.UpdateTree(root);
        Assert.That(fired, Is.EqualTo(2), "LayoutUpdated should fire again after a new invalidation settles");
    }

    // Phase 5: the dirty-queue model must scale - a clean frame and a single-leaf arrange cost the same regardless of
    // tree size (the old full walk visited every node every frame).
    [Test]
    public void LargeTree_CleanFrameAndSingleLeafArrangeDoNotScaleWithSize()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        Border target = null;
        for (var i = 0; i < 500; i++)
        {
            var child = new Border { Width = 40, Height = 10 };
            if (i == 250) target = child;
            stack.Children.Add(child);
        }
        var root = new Border { Width = 100, Height = 5000, Child = stack };

        WindowExtension.UpdateTree(root);   // settle the 500-node tree

        var measure0 = MeasurableUIComponent.TotalMeasureCalls;
        var arrange0 = MeasurableUIComponent.TotalArrangeCalls;
        WindowExtension.UpdateTree(root);   // clean frame

        Assert.Multiple(() =>
        {
            Assert.That(MeasurableUIComponent.TotalMeasureCalls - measure0, Is.EqualTo(0), "clean frame measured in a large tree");
            Assert.That(MeasurableUIComponent.TotalArrangeCalls - arrange0, Is.EqualTo(0), "clean frame arranged in a large tree");

            // Arrange-only invalidation of one leaf re-arranges exactly that leaf - not the 499 siblings.
            var arrange1 = MeasurableUIComponent.TotalArrangeCalls;
            target.InvalidateArrange();
            WindowExtension.UpdateTree(root);
            Assert.That(MeasurableUIComponent.TotalArrangeCalls - arrange1, Is.EqualTo(1), "a single leaf arrange must not scale with tree size");
        });
    }
}
