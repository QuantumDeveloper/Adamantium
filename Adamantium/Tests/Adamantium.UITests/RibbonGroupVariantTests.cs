using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Controls;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>How a group's ways of being drawn are DERIVED from what its commands allow, rather than written out as a
/// matrix. The group steps as one; what varies per command is which step it sits out.</summary>
[TestFixture]
public class RibbonGroupVariantTests
{
    private static (RibbonSize Max, RibbonCollapseThreshold ToMedium, RibbonCollapseThreshold ToSmall) Command(
        RibbonSize max = RibbonSize.Large,
        RibbonCollapseThreshold toMedium = RibbonCollapseThreshold.WhenGroupIsMedium,
        RibbonCollapseThreshold toSmall = RibbonCollapseThreshold.WhenGroupIsSmall) => (max, toMedium, toSmall);

    private static string Shape(RibbonGroupVariant v) =>
        v.IsCollapsed ? "collapsed" : string.Concat(v.Sizes.Select(s => s.ToString()[0]));

    private static IReadOnlyList<string> Shapes(
        params (RibbonSize, RibbonCollapseThreshold, RibbonCollapseThreshold)[] commands) =>
        RibbonGroupVariant.Generate(commands).Select(Shape).ToArray();

    // The roomiest variant is what every command's author asked for - shrinking starts from the intent and only takes
    // away.
    [Test]
    public void TheFirstVariant_IsEveryCommandAtItsMaxSize()
    {
        var shapes = Shapes(Command(RibbonSize.Large), Command(RibbonSize.Medium), Command(RibbonSize.Medium));

        Assert.That(shapes[0], Is.EqualTo("LMM"));
    }

    // Whatever the commands, there is always a last resort - and it is the same one.
    [Test]
    public void TheLastVariant_IsAlwaysTheCollapsedOne()
    {
        var variants = RibbonGroupVariant.Generate([Command(RibbonSize.Small)]);

        Assert.Multiple(() =>
        {
            Assert.That(variants[^1].IsCollapsed, Is.True);
            Assert.That(variants.Count(v => v.IsCollapsed), Is.EqualTo(1), "and it is offered once");
        });
    }

    // The group steps down as ONE. Lowering commands individually offers more widths but reads as broken: a column ends
    // up with a labelled row stacked over a bare icon.
    [Test]
    public void TheWholeGroupStepsDownTogether()
    {
        var shapes = Shapes(Command(), Command(), Command());

        Assert.That(shapes, Is.EqualTo(new[] { "LLL", "MMM", "SSS", "collapsed" }));
    }

    // A command whose author asked for less starts lower and never grows to meet the others: the authored sizes are a
    // composition - a large Paste beside small Cut/Copy - and stepping preserves it.
    [Test]
    public void AuthoredSizesKeepTheirRelation()
    {
        var shapes = Shapes(Command(RibbonSize.Large), Command(RibbonSize.Medium), Command(RibbonSize.Medium));

        Assert.That(shapes, Is.EqualTo(new[] { "LMM", "MMM", "SSS", "collapsed" }));
    }

    // THE reason the thresholds exist: a command nobody recognises without its words sits the last step out while
    // everything beside it takes it.
    [Test]
    public void ACommandThatNeverGoesSmall_KeepsItsLabelToTheEnd()
    {
        var shapes = Shapes(
            Command(toSmall: RibbonCollapseThreshold.Never),
            Command());

        Assert.Multiple(() =>
        {
            Assert.That(shapes, Is.EqualTo(new[] { "LL", "MM", "MS", "collapsed" }));
            Assert.That(shapes, Has.None.StartsWith("S"), "the first command never reaches Small");
        });
    }

    // ...and the other way: a command can give up its label at the FIRST step, before the group asks everyone else to.
    [Test]
    public void ACommandCanGiveUpEverythingAtTheFirstStep()
    {
        var shapes = Shapes(
            Command(toSmall: RibbonCollapseThreshold.WhenGroupIsMedium),
            Command());

        Assert.That(shapes, Is.EqualTo(new[] { "LL", "SM", "SS", "collapsed" }));
    }

    // A command that follows neither step is fixed for good - how an author pins the one command that must stay as it is.
    [Test]
    public void ACommandThatFollowsNeitherStep_NeverChanges()
    {
        var shapes = Shapes(
            Command(toMedium: RibbonCollapseThreshold.Never, toSmall: RibbonCollapseThreshold.Never),
            Command());

        Assert.That(shapes, Is.EqualTo(new[] { "LL", "LM", "LS", "collapsed" }));
    }

    // A step that changes nothing is not offered: a group whose commands all sit both steps out has ONE layout and then
    // collapses, not three identical ones the search would walk through for nothing.
    [Test]
    public void AStepThatChangesNothing_IsNotOffered()
    {
        var shapes = Shapes(
            Command(toMedium: RibbonCollapseThreshold.Never, toSmall: RibbonCollapseThreshold.Never),
            Command(toMedium: RibbonCollapseThreshold.Never, toSmall: RibbonCollapseThreshold.Never));

        Assert.That(shapes, Is.EqualTo(new[] { "LL", "collapsed" }));
    }

    // A threshold reached by a command already drawn small must not GROW it - the steps only ever take away.
    [Test]
    public void AThresholdNeverGrowsACommand()
    {
        var shapes = Shapes(Command(RibbonSize.Small));

        Assert.That(shapes, Is.EqualTo(new[] { "S", "collapsed" }));
    }
}
