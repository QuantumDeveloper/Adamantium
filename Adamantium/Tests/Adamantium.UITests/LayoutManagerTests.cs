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
        public Vector2 PointToClient(Vector2 point) => point;
        public Vector2 PointToScreen(Vector2 point) => point;
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

    // F1: a per-frame layout time budget defers work past the deadline to the next frame, and that deferred work
    // completes over subsequent frames. A zero budget processes ~one node per phase per pass (with forward progress).
    [Test]
    public void FrameBudget_DefersWorkAndCompletesOverFrames()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        var children = new List<Border>();
        for (var i = 0; i < 20; i++)
        {
            var b = new Border { Width = 40, Height = 10 };
            children.Add(b);
            stack.Children.Add(b);
        }
        var root = new Border { Width = 100, Height = 1000, Child = stack };
        WindowExtension.UpdateTree(root);   // settle

        foreach (var b in children) b.InvalidateArrange();   // 20 independent dirty arrange entries

        var savedBudget = LayoutManager.FrameBudget;
        try
        {
            LayoutManager.FrameBudget = TimeSpan.Zero;   // process ~one node per phase per pass
            var arrangeBefore = MeasurableUIComponent.TotalArrangeCalls;
            WindowExtension.UpdateTree(root);
            var firstPass = MeasurableUIComponent.TotalArrangeCalls - arrangeBefore;

            Assert.Multiple(() =>
            {
                Assert.That(firstPass, Is.GreaterThan(0), "forward progress: at least one node processed under a zero budget");
                Assert.That(firstPass, Is.LessThan(20), "a zero budget defers most work to later frames");
            });

            for (var f = 0; f < 40 && children.Any(b => !((IMeasurableComponent)b).IsArrangeValid); f++)
                WindowExtension.UpdateTree(root);

            Assert.That(children.All(b => ((IMeasurableComponent)b).IsArrangeValid), Is.True,
                "budget-deferred work completes over subsequent frames");
        }
        finally { LayoutManager.FrameBudget = savedBudget; }
    }

    // F1 refinement: under a budget, on-screen dirty nodes are processed before off-screen ones (the off-screen work
    // is what gets deferred).
    [Test]
    public void FrameBudget_ProcessesVisibleNodesBeforeOffScreen()
    {
        var root = new TestWindowRoot { Width = 200, Height = 200, ClientWidth = 200, ClientHeight = 200 };
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        root.Children.Add(stack);
        var kids = new List<Border>();
        for (var i = 0; i < 8; i++) { var b = new Border { Width = 50, Height = 50 }; kids.Add(b); stack.Children.Add(b); }
        WindowExtension.UpdateTree(root);   // settle: kids 0-3 sit in the 200px viewport, 4-7 below it

        foreach (var b in kids) b.InvalidateArrange();

        var savedBudget = LayoutManager.FrameBudget;
        try
        {
            LayoutManager.FrameBudget = TimeSpan.Zero;   // ~one node/pass
            for (var p = 0; p < 3; p++) WindowExtension.UpdateTree(root);   // 3 passes (< the anti-starvation threshold)

            Assert.Multiple(() =>
            {
                for (var i = 4; i < 8; i++)
                    Assert.That(((IMeasurableComponent)kids[i]).IsArrangeValid, Is.False, $"off-screen kid {i} must be deferred behind the visible ones");
                Assert.That(Enumerable.Range(0, 4).Count(i => ((IMeasurableComponent)kids[i]).IsArrangeValid), Is.GreaterThan(0),
                    "on-screen kids are processed first");
            });
        }
        finally { LayoutManager.FrameBudget = savedBudget; }
    }

    // F1 refinement (anti-starvation): a sustained backlog must not crawl one node per frame forever - after a few
    // budget-capped passes the manager drops the budget for one pass and clears it.
    [Test]
    public void FrameBudget_AntiStarvation_DrainsBacklogInBoundedPasses()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        var kids = new List<Border>();
        for (var i = 0; i < 30; i++) { var b = new Border { Width = 40, Height = 10 }; kids.Add(b); stack.Children.Add(b); }
        var root = new Border { Width = 100, Height = 2000, Child = stack };
        WindowExtension.UpdateTree(root);

        foreach (var b in kids) b.InvalidateArrange();

        var savedBudget = LayoutManager.FrameBudget;
        try
        {
            LayoutManager.FrameBudget = TimeSpan.Zero;   // 1 node/pass without anti-starvation -> would need ~30 passes
            var passes = 0;
            while (kids.Any(b => !((IMeasurableComponent)b).IsArrangeValid) && passes < 50)
            {
                WindowExtension.UpdateTree(root);
                passes++;
            }
            Assert.Multiple(() =>
            {
                Assert.That(kids.All(b => ((IMeasurableComponent)b).IsArrangeValid), Is.True, "the whole backlog drains");
                Assert.That(passes, Is.LessThanOrEqualTo(6), "anti-starvation clears the backlog in a bounded number of passes, not one-per-frame");
            });
        }
        finally { LayoutManager.FrameBudget = savedBudget; }
    }

    // F1 deferred-render guarantee (emergent): a never-arranged node has empty Bounds, so the renderer draws nothing for
    // it until it's laid out - a budget-deferred brand-new subtree shows nothing rather than garbage.
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
