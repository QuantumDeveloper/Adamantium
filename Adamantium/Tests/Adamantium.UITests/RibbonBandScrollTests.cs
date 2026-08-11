using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

// The last resort of the adaptive band (docs/RIBBON_PLAN.md §3.4): when everything has been shrunk and collapsed and it
// still does not fit, the row scrolls - by WHOLE GROUPS, because a half-shown group reads as damage rather than as
// "there is more".
[TestFixture]
public class RibbonBandScrollTests
{
    // A group of a stated width, so the arithmetic under test is the panel's and not the ladder's.
    private static RibbonGroup Group(double width)
    {
        var group = new RibbonGroup { Header = "G", Width = width, MinWidth = width, MaxWidth = width };
        return group;
    }

    private static RibbonGroupsPanel Row(double viewport, params double[] widths)
    {
        var row = new RibbonGroupsPanel();
        foreach (var width in widths) row.Children.Add(Group(width));

        ((IMeasurableComponent)row).Measure(new Size(double.PositiveInfinity, 100));
        ((IMeasurableComponent)row).Arrange(new Rect(0, 0, viewport, 100));
        return row;
    }

    [Test]
    public void ARowThatFitsScrollsNowhere()
    {
        var row = Row(300, 100, 100);

        Assert.Multiple(() =>
        {
            Assert.That(row.CanScrollBack, Is.False);
            Assert.That(row.CanScrollForward, Is.False);
        });
    }

    [Test]
    public void ARowTooWideOffersTheWayForward()
    {
        var row = Row(150, 100, 100, 100);

        Assert.Multiple(() =>
        {
            Assert.That(row.CanScrollForward, Is.True);
            Assert.That(row.CanScrollBack, Is.False, "nothing is off the near edge yet");
        });
    }

    [Test]
    public void ItStepsByAWholeGroup()
    {
        var row = Row(150, 100, 100, 100);

        row.ScrollForward();

        Assert.That(row.Offset, Is.EqualTo(100), "the next group's edge, not a pixel amount");
    }

    [Test]
    public void AndBackTheSameWay()
    {
        var row = Row(150, 100, 100, 100);
        row.ScrollForward();
        row.ScrollForward();

        row.ScrollBack();

        Assert.That(row.Offset, Is.EqualTo(100));
    }

    [Test]
    public void ItNeverRunsPastTheEnd()
    {
        var row = Row(150, 100, 100, 100);

        for (var i = 0; i < 10; i++) row.ScrollForward();

        Assert.Multiple(() =>
        {
            Assert.That(row.Offset, Is.EqualTo(150), "the last group's edge, clamped to what is left");
            Assert.That(row.CanScrollForward, Is.False);
            Assert.That(row.CanScrollBack, Is.True);
        });
    }

    [Test]
    public void GrowingTheWindowPullsTheRowBack()
    {
        var row = Row(150, 100, 100, 100);
        for (var i = 0; i < 10; i++) row.ScrollForward();

        // The window widens: the row now fits, and a view left hanging past its end would show a gap.
        ((IMeasurableComponent)row).Arrange(new Rect(0, 0, 400, 100));

        Assert.Multiple(() =>
        {
            Assert.That(row.Offset, Is.Zero);
            Assert.That(row.CanScrollForward, Is.False);
        });
    }
}
