using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Handing a live control to a NEW parent without taking it out of the old one first. Every control-level move does
/// this - an items presenter builds a fresh items panel and fills it with the containers the previous panel still
/// lists, a docking rebuild moves tabs between groups - so the answer decides whether one control can end up being laid
/// out twice, by two parents, at two different places.
/// <para>Companion to <see cref="VisualChildDetachTests"/>, which covers the tidy path (remove, then add).</para>
/// </summary>
[TestFixture]
public class VisualChildReparentTests
{
    private static StackPanel Laid(params IMeasurableComponent[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children) panel.Children.Add(child);

        panel.Measure(new Size(400, 200));
        panel.Arrange(new Rect(0, 0, 400, 200));
        return panel;
    }

    /// <summary>The question in its smallest form: after the second panel takes the child, does the first still list it?
    /// A control belongs to one parent, so a stale entry in the old collection is a second claim on it.</summary>
    [Test]
    public void AddingToASecondPanel_TakesItOutOfTheFirst()
    {
        var moved = new Border { Width = 50, Height = 20 };
        var from = Laid(moved);
        var to = Laid();

        to.Children.Add(moved);   // NOT removed from `from` first - the case every rebuild actually takes

        Assert.Multiple(() =>
        {
            Assert.That(moved.VisualParent, Is.SameAs(to), "the new parent owns it");
            Assert.That(from.Children, Does.Not.Contain(moved), "and the old one no longer lists it");
            Assert.That(from.VisualChildren, Does.Not.Contain(moved));
        });
    }

    /// <summary>What the stale entry costs, if it is there: the old parent goes on measuring and arranging the child,
    /// so its final position is whichever parent happened to run last - which is what an overlap looks like on screen.
    /// </summary>
    [Test]
    public void AfterAMove_OnlyTheNewParentLaysTheChildOut()
    {
        var first = new Border { Width = 50, Height = 20 };
        var moved = new Border { Width = 50, Height = 20 };
        var from = Laid(first, moved);       // moved sits at x=50 here
        var to = Laid();

        to.Children.Add(moved);              // in `to` it is the only child, so it belongs at x=0

        // The OLD parent laid out last, deliberately: if it still claims the child, its answer is the one left standing.
        // Whose turn comes last is an accident of tree order, which is exactly why it must not decide anything.
        to.Measure(new Size(400, 200));
        to.Arrange(new Rect(0, 0, 400, 200));
        from.Measure(new Size(400, 200));
        from.Arrange(new Rect(0, 0, 400, 200));

        Assert.That(moved.Bounds.X, Is.EqualTo(0).Within(0.5),
            "the old parent must not still be placing it - two parents laying one control out is an overlap");
    }
}
