using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Taking a child out of a panel has to DETACH it: drop the parent link and stop it being laid out. Anything less
/// leaves an orphan that the layout pass keeps visiting - it goes on measuring and arranging at the size it had, while
/// whatever replaced it may never be visited at all.
/// <para>Written on bare panels on purpose. This was found through docking, which is simply the first thing in the
/// engine that MOVES live controls between parents - everything else builds its tree once and leaves it alone, so the
/// removal path was never really exercised.</para>
/// </summary>
[TestFixture]
public class VisualChildDetachTests
{
    private static StackPanel Laid(params IMeasurableComponent[] children)
    {
        var panel = new StackPanel();
        foreach (var child in children) panel.Children.Add(child);

        panel.Measure(new Size(200, 200));
        panel.Arrange(new Rect(0, 0, 200, 200));
        return panel;
    }

    [Test]
    public void RemovingAChild_DropsItsParent()
    {
        var kept = new Border();
        var removed = new Border();
        var panel = Laid(kept, removed);

        panel.Children.Remove(removed);

        Assert.Multiple(() =>
        {
            Assert.That(removed.VisualParent, Is.Null, "a removed child must not still point at the panel");
            Assert.That(panel.VisualChildren, Does.Not.Contain(removed));
            Assert.That(kept.VisualParent, Is.SameAs(panel), "its neighbour is untouched");
        });
    }

    /// <summary>Clear takes a different route through the collection - a Reset carries no list of what left - so it is
    /// asserted separately. This is the call a control makes when it refills itself wholesale.</summary>
    [Test]
    public void ClearingChildren_DropsTheirParents()
    {
        var first = new Border();
        var second = new Border();
        var panel = Laid(first, second);

        panel.Children.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(first.VisualParent, Is.Null);
            Assert.That(second.VisualParent, Is.Null);
            Assert.That(panel.VisualChildren, Is.Empty);
        });
    }

    /// <summary>The point of detaching: a child that has left must stop being laid out, and the one that took its place
    /// must start. An orphan still being arranged is what draws over the live tree.</summary>
    [Test]
    public void AfterAReplacement_OnlyTheLiveChildIsLaidOut()
    {
        var gone = new Border { Width = 50, Height = 20 };
        var panel = Laid(gone);

        panel.Children.Clear();
        var live = new Border { Width = 80, Height = 30 };
        panel.Children.Add(live);

        panel.Measure(new Size(200, 200));
        panel.Arrange(new Rect(0, 0, 200, 200));

        Assert.Multiple(() =>
        {
            Assert.That(live.Bounds.Width, Is.EqualTo(80).Within(0.5), "the child that is there gets laid out");
            Assert.That(gone.VisualParent, Is.Null, "and the one that left is no longer part of anything");
        });
    }

    /// <summary>Moving a live control from one panel to another - what a docking gesture does - must leave it belonging
    /// to exactly one of them.</summary>
    [Test]
    public void MovingAChildBetweenPanels_LeavesOneParent()
    {
        var moved = new Border();
        var from = Laid(moved);
        var to = Laid();

        from.Children.Remove(moved);
        to.Children.Add(moved);

        Assert.Multiple(() =>
        {
            Assert.That(moved.VisualParent, Is.SameAs(to));
            Assert.That(from.VisualChildren, Does.Not.Contain(moved));
            Assert.That(to.VisualChildren, Does.Contain(moved));
        });
    }
}
