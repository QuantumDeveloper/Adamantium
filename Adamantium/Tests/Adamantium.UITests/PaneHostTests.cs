using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// PaneHost's whole job is arithmetic, so it is checked as arithmetic - rectangles, no GPU. Each child states ONE
/// length: so many pixels, or a weight in what is left over. That is a Grid's rule, and it is here for a Grid's reason -
/// a size described by two numbers at once (a share AND a pixel hint) has to be kept in step through every split, move
/// and drag, and each of those is a place the two drift apart.
/// </summary>
[TestFixture]
public class PaneHostTests
{
    private static Border Star(double weight = 1)
    {
        var border = new Border();
        PaneHost.SetPaneLength(border, PaneLength.Stars(weight));
        return border;
    }

    private static Border Fixed(double pixels)
    {
        var border = new Border();
        PaneHost.SetPaneLength(border, PaneLength.Pixels(pixels));
        return border;
    }

    [Test]
    public void StarredChildrenSplitWhatIsLeft_MinusTheDividers()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 4 };
        var left = Star(0.75);
        var right = Star(0.25);
        split.Children.Add(left);
        split.Children.Add(right);

        split.Measure(new Size(404, 100));
        split.Arrange(new Rect(0, 0, 404, 100));

        // 404 minus one 4px divider = 400 to share: 300 and 100.
        Assert.Multiple(() =>
        {
            Assert.That(left.Bounds.Width, Is.EqualTo(300).Within(0.5));
            Assert.That(right.Bounds.X, Is.EqualTo(304).Within(0.5), "the second child starts after the divider");
            Assert.That(right.Bounds.Width, Is.EqualTo(100).Within(0.5));
            Assert.That(left.Bounds.Height, Is.EqualTo(100).Within(0.5), "across the axis a child fills the panel");
        });
    }

    [Test]
    public void LastChildTakesWhatIsLeft_SoNoHairlineRemains()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        // Thirds: any per-child rounding leaves a sliver at the right edge.
        split.Children.Add(Star());
        split.Children.Add(Star());
        var last = Star();
        split.Children.Add(last);

        split.Measure(new Size(100, 50));
        split.Arrange(new Rect(0, 0, 100, 50));

        Assert.That(last.Bounds.X + last.Bounds.Width, Is.EqualTo(100).Within(1e-9), "the far edge is reached exactly");
    }

    [Test]
    public void VerticalSplit_StacksAlongTheOtherAxis()
    {
        var split = new PaneHost { Orientation = Orientation.Vertical, DividerThickness = 0 };
        var top = Star(0.25);
        var bottom = Star(0.75);
        split.Children.Add(top);
        split.Children.Add(bottom);

        split.Measure(new Size(200, 400));
        split.Arrange(new Rect(0, 0, 200, 400));

        Assert.Multiple(() =>
        {
            Assert.That(top.Bounds.Height, Is.EqualTo(100).Within(0.5));
            Assert.That(bottom.Bounds.Y, Is.EqualTo(100).Within(0.5));
            Assert.That(bottom.Bounds.Width, Is.EqualTo(200).Within(0.5), "across the axis it fills");
        });
    }

    /// <summary>A splitter occupies the gap the layout already reserves; it is not content and takes no share.</summary>
    [Test]
    public void SplitterSitsInTheGap_WithoutTakingAShare()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 8 };
        var left = Star();
        var splitter = new PaneSplitter();
        var right = Star();
        split.Children.Add(left);
        split.Children.Add(splitter);
        split.Children.Add(right);

        split.Measure(new Size(208, 100));
        split.Arrange(new Rect(0, 0, 208, 100));

        // 208 minus one 8px gap = 200 shared: 100 each, with the grip filling the gap between them.
        Assert.Multiple(() =>
        {
            Assert.That(left.Bounds.Width, Is.EqualTo(100).Within(0.5), "the splitter is not counted as a third share");
            Assert.That(splitter.Bounds.X, Is.EqualTo(100).Within(0.5), "it fills the reserved gap");
            Assert.That(splitter.Bounds.Width, Is.EqualTo(8).Within(0.5));
            Assert.That(right.Bounds.X, Is.EqualTo(108).Within(0.5));
            Assert.That(right.Bounds.X + right.Bounds.Width, Is.EqualTo(208).Within(0.5));
        });
    }

    /// <summary>"The console is 160 tall" means 160 - taken off the top, with what remains going to the stars.</summary>
    [Test]
    public void AFixedChild_TakesItsPixels_AndTheStarsShareTheRest()
    {
        var split = new PaneHost { Orientation = Orientation.Vertical, DividerThickness = 0 };
        var documents = Star();
        var console = Fixed(160);
        split.Children.Add(documents);
        split.Children.Add(console);

        split.Measure(new Size(300, 500));
        split.Arrange(new Rect(0, 0, 300, 500));

        Assert.Multiple(() =>
        {
            Assert.That(console.Bounds.Height, Is.EqualTo(160).Within(0.5), "the number that was stated, not a share of it");
            Assert.That(documents.Bounds.Height, Is.EqualTo(340).Within(0.5));
        });
    }

    /// <summary>The stars keep their own weights against each other while a fixed sibling takes its pixels - the fixed
    /// one is not a third equal party.</summary>
    [Test]
    public void AFixedSibling_DoesNotFlattenTheStarsIntoEqualSlices()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        var left = Star(0.25);
        var centre = Star(0.75);
        var inspector = Fixed(100);
        split.Children.Add(left);
        split.Children.Add(centre);
        split.Children.Add(inspector);

        split.Measure(new Size(500, 100));
        split.Arrange(new Rect(0, 0, 500, 100));

        Assert.Multiple(() =>
        {
            Assert.That(inspector.Bounds.Width, Is.EqualTo(100).Within(0.5));
            Assert.That(left.Bounds.Width, Is.EqualTo(100).Within(0.5), "a quarter of the 400 that is left, not a third of it");
            Assert.That(centre.Bounds.Width, Is.EqualTo(300).Within(0.5));
        });
    }

    /// <summary>
    /// THE reason for fixed lengths: resizing the window must not resize a docked panel. An inspector told to be 240
    /// wide stays 240 wide while the centre absorbs everything the window gains or loses - which is what every editor
    /// does, and what a pure share can never do.
    /// </summary>
    [Test]
    public void ResizingTheHost_MovesTheStars_AndLeavesTheFixedAlone()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        var documents = Star();
        var inspector = Fixed(240);
        split.Children.Add(documents);
        split.Children.Add(inspector);

        split.Measure(new Size(1000, 100));
        split.Arrange(new Rect(0, 0, 1000, 100));

        split.Measure(new Size(1400, 100));
        split.Arrange(new Rect(0, 0, 1400, 100));

        Assert.Multiple(() =>
        {
            Assert.That(inspector.Bounds.Width, Is.EqualTo(240).Within(0.5), "the panel keeps the width it was given");
            Assert.That(documents.Bounds.Width, Is.EqualTo(1160).Within(0.5), "the centre takes the whole difference");
        });
    }

    /// <summary>
    /// And the other half of it: taking a pane OUT of the row must not resize the ones that stay fixed. The space it
    /// leaves goes to the stars - the panes that said they would take whatever is left.
    /// </summary>
    [Test]
    public void RemovingAChild_GivesItsSpaceToTheStars_NotToTheFixed()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        var documents = Star();
        var inspector = Fixed(240);
        var console = Star();
        split.Children.Add(documents);
        split.Children.Add(inspector);
        split.Children.Add(console);

        split.Measure(new Size(1000, 100));
        split.Arrange(new Rect(0, 0, 1000, 100));

        split.Children.Remove(console);
        split.Measure(new Size(1000, 100));
        split.Arrange(new Rect(0, 0, 1000, 100));

        Assert.Multiple(() =>
        {
            Assert.That(inspector.Bounds.Width, Is.EqualTo(240).Within(0.5), "the fixed panel did not move an inch");
            Assert.That(documents.Bounds.Width, Is.EqualTo(760).Within(0.5), "the star took what was freed");
        });
    }

    /// <summary>A fixed child cannot eat the whole row: the stars keep standing room, or there would be no edge left to
    /// grab and drag them back out.</summary>
    [Test]
    public void AFixedChildBiggerThanTheRow_IsCutBackToLeaveTheStarsStandingRoom()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0, MinFraction = 0.1 };
        var documents = Star();
        var greedy = Fixed(5000);
        split.Children.Add(documents);
        split.Children.Add(greedy);

        split.Measure(new Size(1000, 100));
        split.Arrange(new Rect(0, 0, 1000, 100));

        Assert.That(documents.Bounds.Width, Is.EqualTo(100).Within(0.5), "a tenth of the row is left to stand in");
    }

    [Test]
    public void ChildrenThatSaidNothing_ShareEqually()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        var a = new Border();
        var b = new Border();
        split.Children.Add(a);
        split.Children.Add(b);

        split.Measure(new Size(300, 100));
        split.Arrange(new Rect(0, 0, 300, 100));

        Assert.That(a.Bounds.Width, Is.EqualTo(150).Within(0.5), "saying nothing means one share of the leftovers");
    }
}
