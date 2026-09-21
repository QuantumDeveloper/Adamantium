using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// A live stand shows one figure at a time by collapsing the others, and drives its SIZE from a slider. Growing the
// figure has to GROW it in place, not slide it out of the panel - which is what a stand does when the box it is centred
// in is sized by something other than the figure on show.
[TestFixture]
public class PolygonResizeLayoutTests
{
    private static PointsCollection Triangle(double w, double h) =>
        new([new Vector2(w / 2, 0), new Vector2(w, h), new Vector2(0, h)]);

    // The stand's own shape: a fixed panel, a centred box, and three figures of which one is visible.
    private static (Polygon shape, Border panel, Window window) Stand(double w, double h)
    {
        var shape = new Polygon
        {
            Points = Triangle(w, h),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var hidden = new Adamantium.UI.Controls.Shapes.Rectangle { Width = w, Height = h, Visibility = Visibility.Collapsed };

        var box = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        box.Children.Add(hidden);
        box.Children.Add(shape);

        var panel = new Border { Width = 360, Height = 240, Child = box };
        var window = new Window { Width = 500, Height = 400, Content = panel };
        for (var i = 0; i < 6; i++) WindowExtension.UpdateTree(window);

        return (shape, panel, window);
    }

    [Test]
    public void TheFigureGrowsWithItsPoints()
    {
        var (small, _, _) = Stand(120, 100);
        var (large, _, _) = Stand(240, 200);

        Assert.That(large.Bounds.Width, Is.GreaterThan(small.Bounds.Width * 1.5),
            $"the points doubled but the figure measured {small.Bounds.Width} -> {large.Bounds.Width}");
    }

    // Built at a size, the box around the figure is right. The stand instead REPLACES the points as a slider moves, and
    // that is the path that matters: if the new points do not re-measure the box the figure sits in, the figure is
    // arranged into the STALE slot - it grows out of one corner and walks off the panel instead of scaling in place.
    [Test]
    public void ReplacingThePointsResizesTheBoxAroundTheFigure()
    {
        var (shape, panel, window) = Stand(120, 200);
        var box = shape.VisualParent;
        var before = box.Bounds;

        shape.Points = Triangle(300, 200);
        for (var i = 0; i < 6; i++) WindowExtension.UpdateTree(window);

        TestContext.Out.WriteLine($"box {before} -> {box.Bounds}, shape {shape.Bounds}");
        Assert.That(box.Bounds.Width, Is.EqualTo(300).Within(1.0),
            $"the box stayed {box.Bounds.Width} wide after the points grew to 300 - the figure is arranged into a stale slot");
    }

    [Test]
    public void TheFigureStaysCentredInThePanelAsItGrows()
    {
        foreach (var w in new[] { 120.0, 200.0, 320.0 })
        {
            var (shape, panel, window) = Stand(w, 200);
            var box = shape.VisualParent;
            var boxCentre = box.Bounds.X + box.Bounds.Width / 2;
            var panelCentre = panel.Bounds.Width / 2;

            Assert.That(boxCentre, Is.EqualTo(panelCentre).Within(1.0),
                $"at width {w} the figure's box sits at {boxCentre}, the panel's middle is {panelCentre}");
        }
    }

    // The stand switches which figure is on by flipping Visibility. If collapsing the old one does not re-measure the
    // box, the box keeps the size of the figure that is no longer shown - and every later resize is arranged into that.
    [Test]
    public void SwitchingWhichFigureIsShownResizesTheBox()
    {
        var shape = new Polygon
        {
            Points = Triangle(120, 200),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        var other = new Adamantium.UI.Controls.Shapes.Rectangle { Width = 320, Height = 200 };

        var box = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        box.Children.Add(other);
        box.Children.Add(shape);
        var panel = new Border { Width = 360, Height = 240, Child = box };
        var window = new Window { Width = 500, Height = 400, Content = panel };
        for (var i = 0; i < 6; i++) WindowExtension.UpdateTree(window);

        var before = box.Bounds;
        other.Visibility = Visibility.Collapsed;
        shape.Visibility = Visibility.Visible;
        for (var i = 0; i < 6; i++) WindowExtension.UpdateTree(window);

        TestContext.Out.WriteLine($"box {before} -> {box.Bounds}, shape {shape.Bounds}");
        Assert.That(box.Bounds.Width, Is.EqualTo(120).Within(1.0),
            $"the box stayed {box.Bounds.Width} wide - it is still sized by the figure that was switched OFF");
    }

    // The one thing the stand actually relies on: a figure SMALLER than the box it sits in has to be centred in it, not
    // pinned to a corner. A figure that keeps its proportions never fills the box on both axes, so if this fails the
    // figure slides along the box's edge every time the box is resized.
    [Test]
    public void AFigureSmallerThanItsBoxIsCentredInIt()
    {
        var shape = new Polygon
        {
            Points = Triangle(120, 200),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var box = new Grid { Width = 320, Height = 200 };
        box.Children.Add(shape);
        var window = new Window { Width = 500, Height = 400, Content = box };
        for (var i = 0; i < 6; i++) WindowExtension.UpdateTree(window);

        TestContext.Out.WriteLine($"box={box.Bounds} shape={shape.Bounds}");
        Assert.That(shape.Bounds.X, Is.EqualTo(100).Within(1.0),
            $"a 120-wide figure in a 320-wide box should start at 100, it starts at {shape.Bounds.X}");
    }

    [Test]
    public void TheFigureStaysInsideThePanel()
    {
        var (shape, panel, window) = Stand(320, 200);

        Assert.That(shape.Bounds.X, Is.GreaterThanOrEqualTo(panel.Bounds.X - 1.0), "the figure hangs off the left");
        Assert.That(shape.Bounds.Right, Is.LessThanOrEqualTo(panel.Bounds.Right + 1.0), "the figure hangs off the right");
    }
}
