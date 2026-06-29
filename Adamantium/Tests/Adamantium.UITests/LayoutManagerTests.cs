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
}
