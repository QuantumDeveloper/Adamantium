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
/// <para>Written while chasing a macOS switch whose thumb sat further from one end of its track than from the other.
/// The verdict it delivers is a NEGATIVE one and worth keeping as such: the placement is identical for a Border and an
/// Ellipse, in a plain Grid, in a rounded track, in a track whose 1px border leaves the thumb a box too short for it,
/// and across a live alignment flip on an already-laid-out tree. The primitive was blamed and is innocent - the theme
/// was including two ToggleSwitch style sets, and the losing one's trigger was still writing the margin.</para>
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

    /// <summary>The track the thumb actually sat in when it looked wrong: a 38x22 border ONE pixel thick, so the box
    /// handed to an 18 thumb with 2 all round is 36x20 - which 18+2+2 does not fit vertically. Two things were changed
    /// at once to cure it (the border went AND the thumb stopped being an Ellipse); this separates them.
    /// <para>Bounds are PARENT-LOCAL - <c>ArrangeCore</c> builds them from the rect the parent passed, and a panel lays
    /// its cells out from its own origin. So these read against the 36-wide inner box, not against the 38 track.</para>
    /// </summary>
    private static Rect InOutlinedTrack(MeasurableUIComponent thumb, HorizontalAlignment alignment)
    {
        thumb.Width = 18;
        thumb.Height = 18;
        thumb.Margin = new Thickness(2);
        thumb.HorizontalAlignment = alignment;
        thumb.VerticalAlignment = VerticalAlignment.Center;

        var host = new Grid();
        ((IContainer)host).AddOrSetChildComponent(thumb);
        var track = new Border
        {
            Width = 38, Height = 22,
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            BorderBrush = new Adamantium.UI.Core.Media.SolidColorBrush(Colors.Black),
            Child = host
        };
        var root = new Border { Width = 200, Height = 100, Child = track };
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);

        return thumb.Bounds;
    }

    [Test]
    public void AnOverConstrainedThumbKeepsItsSizeAndItsInset()
    {
        var borderLeft = InOutlinedTrack(new Border(), HorizontalAlignment.Left);
        var borderRight = InOutlinedTrack(new Border(), HorizontalAlignment.Right);
        var ellipseLeft = InOutlinedTrack(new Adamantium.UI.Controls.Shapes.Ellipse
            { Fill = new Adamantium.UI.Core.Media.SolidColorBrush(Colors.White) }, HorizontalAlignment.Left);
        var ellipseRight = InOutlinedTrack(new Adamantium.UI.Controls.Shapes.Ellipse
            { Fill = new Adamantium.UI.Core.Media.SolidColorBrush(Colors.White) }, HorizontalAlignment.Right);

        TestContext.WriteLine($"border  left={borderLeft}  right={borderRight}");
        TestContext.WriteLine($"ellipse left={ellipseLeft}  right={ellipseRight}");

        // 2 in from either end of the 36-wide inner box, and the explicit 18x18 survives the squeeze - Width/Height are
        // re-applied AFTER the alignment clamp, so the thumb overflows the short box by a hair rather than being
        // flattened. Identical for both primitives: whatever made the switch look wrong, it was not the Ellipse.
        Assert.Multiple(() =>
        {
            Assert.That(borderLeft.X, Is.EqualTo(2).Within(0.01), "border, left");
            Assert.That(36 - borderRight.Right, Is.EqualTo(2).Within(0.01), "border, right");
            Assert.That(ellipseLeft.X, Is.EqualTo(2).Within(0.01), "ellipse, left");
            Assert.That(36 - ellipseRight.Right, Is.EqualTo(2).Within(0.01), "ellipse, right");
            Assert.That(ellipseLeft, Is.EqualTo(borderLeft), "same box, same placement");
            Assert.That(ellipseRight, Is.EqualTo(borderRight), "at either end");
        });
    }

    [Test]
    public void AnEllipseIsHeldOffBothEdgesEqually()
    {
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

    /// <summary>The switch does not BUILD its thumb at an end - it MOVES it there, on a tree that is already laid out.
    /// Everything above sets the alignment before the first pass, which is the one path the real control never takes.
    /// </summary>
    [Test]
    public void FlippingAlignmentOnALaidOutTreeMovesTheChildAllTheWay()
    {
        var child = new Border { Width = 18, Height = 18, Margin = new Thickness(2) };
        child.HorizontalAlignment = HorizontalAlignment.Left;
        child.VerticalAlignment = VerticalAlignment.Center;

        var host = new Grid();
        ((IContainer)host).AddOrSetChildComponent(child);
        var track = new Border { Width = 38, Height = 22, CornerRadius = new CornerRadius(11), Child = host };
        var root = new Border { Width = 200, Height = 100, Child = track };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        var off = child.Bounds;

        child.HorizontalAlignment = HorizontalAlignment.Right;
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(root);
        var on = child.Bounds;

        TestContext.WriteLine($"off: x={off.X} right={off.Right}");
        TestContext.WriteLine($"on:  x={on.X} right={on.Right}");

        Assert.Multiple(() =>
        {
            Assert.That(off.X, Is.EqualTo(2).Within(0.01), "off");
            Assert.That(38 - on.Right, Is.EqualTo(2).Within(0.01), "on, after a live flip");
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
