using Adamantium.Mathematics;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The units of the desktop. These are small on purpose: the value of <see cref="PixelPoint"/> is not what it computes
/// but what it REFUSES to compute - a desktop point and a logical one no longer add up, and the only way across names a
/// scale. What is left to check is that the crossing itself is right, per axis, and that a round trip is exact.
/// </summary>
[TestFixture]
public class PixelPointTests
{
    [Test]
    public void ACrossingUsesTheSCALE_PerAxis()
    {
        var scale = new Vector2(1.5f, 2f);   // anisotropic on purpose: one number would hide an axis swap

        var logical = new PixelPoint(300, 400).ToLogical(scale);

        Assert.Multiple(() =>
        {
            Assert.That(logical.X, Is.EqualTo(200).Within(1e-6));
            Assert.That(logical.Y, Is.EqualTo(200).Within(1e-6));
        });
    }

    /// <summary>A round trip returns the same point to within the precision of the logical side, which is single -
    /// <see cref="Vector2"/> is what layout is measured in. Sub-thousandth of a pixel: nothing on screen can tell, but
    /// it is a real property of the crossing and worth stating rather than discovering.</summary>
    [Test]
    public void ThereAndBack_IsTheSamePoint()
    {
        var scale = new Vector2(1.25f, 1.75f);
        var start = new PixelPoint(640, 480);

        var back = PixelPoint.FromLogical(start.ToLogical(scale), scale);

        Assert.Multiple(() =>
        {
            Assert.That(back.X, Is.EqualTo(start.X).Within(1e-3));
            Assert.That(back.Y, Is.EqualTo(start.Y).Within(1e-3));
        });
    }

    /// <summary>Subtracting two desktop points is the one thing that needs no scale - a distance on the desktop is still
    /// a desktop distance - and it is what every "how far from here to there" in the drag and docking code does.</summary>
    [Test]
    public void TheDistanceBetweenTwoDesktopPointsNeedsNoScale()
    {
        var delta = new PixelPoint(300, 300) - new PixelPoint(120, 100);

        Assert.Multiple(() =>
        {
            Assert.That(delta.X, Is.EqualTo(180));
            Assert.That(delta.Y, Is.EqualTo(200));
        });
    }

    /// <summary>What the torn-off window does: hold the window by a point measured in ITS OWN logical units, and put
    /// that point under the cursor. At 100% the scale is invisible, which is exactly why the bug survived - so the
    /// check is written at 150%, where a forgotten conversion is off by a third of the window.</summary>
    [Test]
    public void HoldingAWindowByItsCaption_LandsUnderTheCursorAtAnyScale()
    {
        var cursor = new PixelPoint(1000, 700);
        var scale = new Vector2(1.5f, 1.5f);
        var grab = PixelPoint.FromLogical(new Vector2(320, 16), scale);   // half of a 640-wide window, half its caption

        var position = cursor - grab;

        Assert.Multiple(() =>
        {
            Assert.That(position.X, Is.EqualTo(1000 - 480).Within(1e-6), "half of 640 logical is 480 physical at 150%");
            Assert.That(position.Y, Is.EqualTo(700 - 24).Within(1e-6));
        });
    }
}
