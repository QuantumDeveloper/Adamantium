using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// PaneHost's whole job is arithmetic, so it is checked as arithmetic - rectangles, no GPU. This is the panel the
/// docking layout stands on, and the reason it is not a Grid: the share lives in ONE place and this only spends it.
/// </summary>
[TestFixture]
public class PaneHostTests
{
    private static Border Child(double fraction)
    {
        var border = new Border();
        if (!double.IsNaN(fraction)) PaneHost.SetFraction(border, fraction);
        return border;
    }

    [Test]
    public void ChildrenTakeTheirShare_MinusTheDividers()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 4 };
        var left = Child(0.75);
        var right = Child(0.25);
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
        split.Children.Add(Child(1.0 / 3));
        split.Children.Add(Child(1.0 / 3));
        var last = Child(1.0 / 3);
        split.Children.Add(last);

        split.Measure(new Size(100, 50));
        split.Arrange(new Rect(0, 0, 100, 50));

        Assert.That(last.Bounds.X + last.Bounds.Width, Is.EqualTo(100).Within(1e-9), "the far edge is reached exactly");
    }

    [Test]
    public void VerticalSplit_StacksAlongTheOtherAxis()
    {
        var split = new PaneHost { Orientation = Orientation.Vertical, DividerThickness = 0 };
        var top = Child(0.25);
        var bottom = Child(0.75);
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
        var left = Child(0.5);
        var splitter = new PaneSplitter();
        var right = Child(0.5);
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

    /// <summary>"The console starts 160 tall" has to mean 160 - not 160 measured against whatever height the window
    /// happened to have on some pass chosen to be the final one.</summary>
    [Test]
    public void PixelHint_IsHonouredExactly_AndWhatIsLeftGoesToTheRest()
    {
        var split = new PaneHost { Orientation = Orientation.Vertical, DividerThickness = 0 };
        var documents = Child(0.5);
        var console = Child(0.5);
        PaneHost.SetDesiredSizeHint(console, 160);
        split.Children.Add(documents);
        split.Children.Add(console);

        split.Measure(new Size(300, 500));
        split.Arrange(new Rect(0, 0, 300, 500));

        Assert.Multiple(() =>
        {
            Assert.That(console.Bounds.Height, Is.EqualTo(160).Within(0.5), "the authored number, not a share of it");
            Assert.That(documents.Bounds.Height, Is.EqualTo(340).Within(0.5));
        });
    }

    /// <summary>A sibling asking for a pixel size must not cost the others their OWN shares - if it did, a splitter
    /// drag between two of them would write shares that the layout then ignored, and the boundary would not follow the
    /// pointer at all.</summary>
    [Test]
    public void HintedSibling_DoesNotFlattenTheOthersIntoEqualSlices()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        var left = Child(0.25);
        var centre = Child(0.75);
        var inspector = Child(double.NaN);
        PaneHost.SetDesiredSizeHint(inspector, 100);
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

    /// <summary>Freezing is what a drag does before its first pixel of movement, and it must be invisible: the shares it
    /// writes have to lay out to exactly what the hints were already producing. Otherwise the boundary jumps away from
    /// the cursor the moment the mouse moves.</summary>
    [Test]
    public void FreezingShares_ReproducesTheSameLayout()
    {
        var split = new PaneHost { Orientation = Orientation.Vertical, DividerThickness = 0 };
        var documents = Child(0.5);
        var console = Child(0.5);
        PaneHost.SetDesiredSizeHint(console, 160);
        split.Children.Add(documents);
        split.Children.Add(console);

        split.Measure(new Size(300, 500));
        split.Arrange(new Rect(0, 0, 300, 500));

        split.FreezeShares();
        split.Measure(new Size(300, 500));
        split.Arrange(new Rect(0, 0, 300, 500));

        Assert.Multiple(() =>
        {
            Assert.That(console.Bounds.Height, Is.EqualTo(160).Within(0.5), "nothing moved");
            Assert.That(PaneHost.GetFraction(console), Is.EqualTo(0.32).Within(1e-6), "and the size now lives in the share");
            Assert.That(PaneHost.GetDesiredSizeHint(console), Is.NaN, "the hint has been spent");
        });
    }

    [Test]
    public void ChildWithoutAShare_GetsAnEqualSlice()
    {
        var split = new PaneHost { Orientation = Orientation.Horizontal, DividerThickness = 0 };
        var a = Child(double.NaN);
        var b = Child(double.NaN);
        split.Children.Add(a);
        split.Children.Add(b);

        split.Measure(new Size(300, 100));
        split.Arrange(new Rect(0, 0, 300, 100));

        Assert.That(a.Bounds.Width, Is.EqualTo(150).Within(0.5), "no share declared still has to land somewhere sane");
    }
}
