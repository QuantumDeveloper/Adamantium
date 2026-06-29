using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Phase 1 of the layout-manager plan: layout is dirty-queue-driven, not a per-frame full-tree walk. These tests assert
// the headline guarantee - a CLEAN frame (nothing invalidated) triggers ZERO Measure/Arrange calls - and that
// invalidating a node DOES schedule work on the next pass, then settles back to zero.
public class LayoutManagerTests
{
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
}
