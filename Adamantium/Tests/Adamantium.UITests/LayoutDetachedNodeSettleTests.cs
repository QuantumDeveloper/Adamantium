using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A root has to be able to SETTLE. Anything that leaves a queue permanently non-empty costs a layout pass
/// every frame for the life of the window - and, because a theme swap waits for every window to settle before it takes
/// the busy overlay down, one such root leaves the loading indicator up forever.</summary>
[TestFixture]
public class LayoutDetachedNodeSettleTests
{
    // The state that occurs whenever a template is rebuilt, a popup closes or a container is recycled: the node was
    // queued for arrange while it was still ours, and left the tree before the pass reached it. Its measure
    // invalidation then resolves to whatever root owns it NOW, so this manager can never make it measure-valid.
    [Test]
    public void ARootStillSettlesWhenAQueuedNodeLeavesItsTree()
    {
        var root = new Grid();
        var child = new Border();
        root.Children.Add(child);

        var manager = LayoutManager.For(root);
        ((IMeasurableComponent)root).Measure(new Size(200, 200));
        ((IMeasurableComponent)root).Arrange(new Rect(0, 0, 200, 200));
        manager.ExecuteLayoutPass();
        Assert.That(manager.IsSettled, Is.True, "precondition: a quiet tree owes nothing");

        ((IMeasurableComponent)child).InvalidateArrange();
        root.Children.Remove(child);
        ((IMeasurableComponent)child).InvalidateMeasure();

        manager.ExecuteLayoutPass();

        Assert.That(manager.IsSettled, Is.True,
            "a node that left the tree must leave the queue - re-queueing it spins the pass forever");
    }

    // The ordinary case the re-queue exists for must keep working: a node still in the tree whose measure was dirtied
    // after its arrange was queued is measured and arranged in the same pass.
    [Test]
    public void ANodeStillInTheTreeIsStillMeasuredAndArranged()
    {
        var root = new Grid();
        var child = new Border();
        root.Children.Add(child);

        var manager = LayoutManager.For(root);
        ((IMeasurableComponent)root).Measure(new Size(200, 200));
        ((IMeasurableComponent)root).Arrange(new Rect(0, 0, 200, 200));
        manager.ExecuteLayoutPass();

        ((IMeasurableComponent)child).InvalidateMeasure();

        manager.ExecuteLayoutPass();

        Assert.Multiple(() =>
        {
            Assert.That(((IMeasurableComponent)child).IsMeasureValid, Is.True);
            Assert.That(((IMeasurableComponent)child).IsArrangeValid, Is.True);
            Assert.That(manager.IsSettled, Is.True);
        });
    }
}
