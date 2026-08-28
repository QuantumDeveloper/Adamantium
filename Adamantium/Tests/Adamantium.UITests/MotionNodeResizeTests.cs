using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A render MOTION NODE bakes its subtree in its OWN space and rides a transform-table slot, so when it moves the render
/// rewrites one matrix and replays the recorded frame instead of walking the window. That is what makes panning a tab
/// strip cheap, and it is only true of a MOVE.
/// <para>A folded docking panel is where it stops being true: the strip does not slide, it turns - wide and short
/// becomes narrow and tall, and every label inside it is re-laid-out in the node's own space. Replaying that from a
/// matrix draws the old shape at the new place, which on the stand was a strip standing above its own panel with the
/// turned labels clipped. It corrected itself as soon as anything forced a fresh walk - clicking a tab - which is the
/// signature of a right layout and a stale recording.</para>
/// </summary>
[TestFixture]
public class MotionNodeResizeTests
{
    private static (bool moved, bool subtreeMoved) Notifications(Border node, Rect from, Rect to)
    {
        var moved = false;
        var subtreeMoved = false;
        void OnMoved(IUIComponent c) { if (ReferenceEquals(c, node)) moved = true; }
        void OnSubtreeMoved(IUIComponent c) { if (ReferenceEquals(c, node)) subtreeMoved = true; }

        node.Bounds = from;
        VisualTreeNotifications.Moved += OnMoved;
        VisualTreeNotifications.SubtreeMoved += OnSubtreeMoved;
        try { node.Bounds = to; }
        finally
        {
            VisualTreeNotifications.Moved -= OnMoved;
            VisualTreeNotifications.SubtreeMoved -= OnSubtreeMoved;
        }
        return (moved, subtreeMoved);
    }

    /// <summary>The case the fast path exists for, and it must stay fast: same size, new position.</summary>
    [Test]
    public void AMotionNodeThatOnlyMOVES_KeepsTheSlotRewrite()
    {
        var node = new Border { IsRenderMotionNode = true };

        var (moved, subtreeMoved) = Notifications(node, new Rect(0, 0, 200, 30), new Rect(0, -40, 200, 30));

        Assert.Multiple(() =>
        {
            Assert.That(subtreeMoved, Is.True, "a pan is one matrix and a replay");
            Assert.That(moved, Is.False, "...and must not take the conservative path");
        });
    }

    /// <summary>And the case that broke: the node's SIZE changed, so its subtree was re-laid-out inside it. That needs
    /// BOTH notifications, and picking one is how it stays broken either way - the slot alone replays the old shape at
    /// the new place, and the re-bake alone leaves the subtree pinned to where the node used to be, because everything
    /// under a motion node is baked RELATIVE to it.</summary>
    [Test]
    public void AMotionNodeThatRESIZES_RewritesItsSlotAndReBakes()
    {
        var node = new Border { IsRenderMotionNode = true };

        // A tab strip folding against a side: wide and short becomes narrow and tall.
        var (moved, subtreeMoved) = Notifications(node, new Rect(0, 0, 200, 30), new Rect(0, 0, 30, 200));

        Assert.Multiple(() =>
        {
            Assert.That(subtreeMoved, Is.True, "the slot still has to be rewritten - the subtree hangs off it");
            Assert.That(moved, Is.True, "and a reshaped node has to be baked again, not replayed");
        });
    }
}
