using Adamantium.Mathematics;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The area turns AUTHORED ZONES into rectangles. Checked as rectangles - no window, no GPU: the author says "inspector
/// on the right, 200 wide" and the only question worth asking is where things actually land.
/// </summary>
[TestFixture]
public class DockingAreaTests
{
    /// <summary>A group with a single pane in it. The pane matters: the model names PANES, so a group holding none has
    /// nothing to place and is dropped - which is right, and is why every fixture here puts one in.</summary>
    private static PaneGroup Group(string name, DockZone zone, double size = double.NaN)
    {
        var group = new PaneGroup { Name = name, Zone = zone, Size = size };
        group.Items.Add(new Pane { Header = name, Id = name });
        return group;
    }

    [Test]
    public void ZonesBecomeRectangles_AndTheLastDeclaredIsOutermost()
    {
        var area = new DockingArea { DividerThickness = 0 };
        var centre = Group("scene", DockZone.Center);
        var right = Group("inspector", DockZone.Right);
        var bottom = Group("console", DockZone.Bottom);
        area.Children.Add(centre);
        area.Children.Add(right);
        area.Children.Add(bottom);

        area.Measure(new Size(1000, 800));
        area.Arrange(new Rect(0, 0, 1000, 800));

        Assert.Multiple(() =>
        {
            // Bottom was declared last, so it spans the full width under everything else.
            Assert.That(bottom.Bounds.Width, Is.EqualTo(1000).Within(0.5), "the outermost split runs the whole way");
            Assert.That(bottom.Bounds.Y + bottom.Bounds.Height, Is.EqualTo(800).Within(0.5), "and reaches the bottom edge");

            // Above it, centre and right share the width.
            Assert.That(centre.Bounds.X, Is.EqualTo(0).Within(0.5));
            Assert.That(right.Bounds.X, Is.GreaterThan(centre.Bounds.X), "the inspector is to the RIGHT of the scene");
            Assert.That(right.Bounds.X + right.Bounds.Width, Is.EqualTo(1000).Within(0.5), "and it ends at the edge");
            Assert.That(centre.Bounds.Height, Is.EqualTo(right.Bounds.Height).Within(0.5), "both stop above the console");
        });
    }

    [Test]
    public void DividersTakeTheirSpaceOutOfTheSharedExtent()
    {
        var area = new DockingArea { DividerThickness = 6 };
        area.Children.Add(Group("scene", DockZone.Center));
        var right = Group("inspector", DockZone.Right);
        area.Children.Add(right);

        area.Measure(new Size(506, 400));
        area.Arrange(new Rect(0, 0, 506, 400));

        Assert.That(right.Bounds.X + right.Bounds.Width, Is.EqualTo(506).Within(0.5),
            "the far edge is still reached exactly - the divider comes out of the middle, not off the end");
    }

    [Test]
    public void ASingleGroup_TakesEverything()
    {
        var area = new DockingArea();
        var only = Group("scene", DockZone.Center);
        area.Children.Add(only);

        area.Measure(new Size(640, 480));
        area.Arrange(new Rect(0, 0, 640, 480));

        Assert.Multiple(() =>
        {
            Assert.That(only.Bounds.Width, Is.EqualTo(640).Within(0.5));
            Assert.That(only.Bounds.Height, Is.EqualTo(480).Within(0.5));
        });
    }
}
