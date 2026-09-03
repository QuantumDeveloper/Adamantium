using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A margin has to hold a child off the edge it is aligned to, at EITHER edge.
/// <para>Written while chasing a macOS switch whose thumb sat symmetrically at one end of its track and not at the
/// other. Two guesses at the markup missed; this measures the layout underneath instead, with no theme involved.</para>
/// </summary>
[TestFixture]
public class AlignedMarginTests
{
    private static Rect Placed(HorizontalAlignment alignment, double cornerRadius = 0)
    {
        var child = new Border { Width = 18, Height = 18, Margin = new Thickness(2) };
        child.HorizontalAlignment = alignment;
        child.VerticalAlignment = VerticalAlignment.Center;

        var host = new Grid();
        ((IContainer)host).AddOrSetChildComponent(child);

        // The switch's thumb sits in a Grid inside a ROUNDED Border, which is the one thing the first version of this
        // test left out.
        var track = new Border
        {
            Width = 38, Height = 22,
            CornerRadius = new CornerRadius(cornerRadius),
            Child = host
        };

        var root = new Border { Width = 200, Height = 100, Child = track };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        return child.Bounds;
    }

    private static Rect PlacedEllipse(HorizontalAlignment alignment)
    {
        var child = new Adamantium.UI.Controls.Shapes.Ellipse
        {
            Width = 18, Height = 18, Margin = new Thickness(2),
            Fill = new Adamantium.UI.Core.Media.SolidColorBrush(Colors.White)
        };
        child.HorizontalAlignment = alignment;
        child.VerticalAlignment = VerticalAlignment.Center;

        var host = new Grid();
        ((IContainer)host).AddOrSetChildComponent(child);

        var track = new Border { Width = 38, Height = 22, CornerRadius = new CornerRadius(11), Child = host };
        var root = new Border { Width = 200, Height = 100, Child = track };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        return child.Bounds;
    }

    [Test]
    public void AnEllipseIsHeldOffBothEdgesEqually()
    {
        // The switch's thumb is an Ellipse, not a Border - the only thing left that differed from the case above.
        var left = PlacedEllipse(HorizontalAlignment.Left);
        var right = PlacedEllipse(HorizontalAlignment.Right);

        TestContext.WriteLine($"ellipse left-aligned: x={left.X} right={left.Right} w={left.Width}");
        TestContext.WriteLine($"ellipse right-aligned: x={right.X} right={right.Right} w={right.Width}");

        Assert.Multiple(() =>
        {
            Assert.That(left.X, Is.EqualTo(2).Within(0.01), "left-aligned");
            Assert.That(38 - right.Right, Is.EqualTo(2).Within(0.01), "right-aligned");
        });
    }

    [Test]
    public void AMarginHoldsAChildOffBothEdgesEqually()
    {
        var left = Placed(HorizontalAlignment.Left, cornerRadius: 11);
        var right = Placed(HorizontalAlignment.Right, cornerRadius: 11);

        TestContext.WriteLine($"left-aligned: x={left.X} right={left.Right} w={left.Width}");
        TestContext.WriteLine($"right-aligned: x={right.X} right={right.Right} w={right.Width}");

        // 38 wide, an 18 child with a 2 margin: 2 in from whichever edge it is aligned to.
        Assert.Multiple(() =>
        {
            Assert.That(left.X, Is.EqualTo(2).Within(0.01), "left-aligned: the margin holds it off the left edge");
            Assert.That(38 - right.Right, Is.EqualTo(2).Within(0.01), "right-aligned: and off the right one");
        });
    }
}
