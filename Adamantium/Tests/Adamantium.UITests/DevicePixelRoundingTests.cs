using System;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Pixel snapping rounds MIDPOINTS one way. At a fractional scale a whole number of DIPs lands on half a
/// pixel - at 150% every integer coordinate does - so essentially every edge is a midpoint case, and the default
/// "round half to even" sends neighbouring edges in OPPOSITE directions by parity. Two plates of the same size then
/// come out a pixel apart on screen.</summary>
[TestFixture]
public class DevicePixelRoundingTests
{
    // What DevicePixels.Edge computes: an absolute coordinate taken to a whole device pixel.
    private static double Edge(double device) => Math.Round(device, MidpointRounding.AwayFromZero);

    [Test]
    public void MidpointsAllGoTheSameWay()
    {
        // 39 and 57 DIP at 150% - the two rows of the ribbon's strip, and the pair that showed the defect.
        Assert.Multiple(() =>
        {
            Assert.That(Edge(39 * 1.5), Is.EqualTo(59), "58.5 must not fall to 58 while 85.5 climbs to 86");
            Assert.That(Edge(57 * 1.5), Is.EqualTo(86));
        });
    }

    // The property that actually matters: edges an EVEN number of pixels apart and edges an ODD number apart must move
    // by the same amount, or two rectangles of equal height end up unequal.
    [Test]
    public void EqualSpansStaySpansWhateverTheParity()
    {
        for (var dip = 1; dip <= 64; dip++)
        {
            var top = Edge(dip * 1.5);
            var bottom = Edge((dip + 18) * 1.5);

            Assert.That(bottom - top, Is.EqualTo(27), $"an 18-DIP band at y={dip} came out {bottom - top} px");
        }
    }

    // The default is what the engine used, and it is what broke: kept as an executable statement of why the rule is
    // stated explicitly, so nobody restores the shorter call.
    [Test]
    public void TheDefaultRuleIsWhatSplitThem()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Math.Round(39 * 1.5), Is.EqualTo(58), "half to even rounds this one down...");
            Assert.That(Math.Round(57 * 1.5), Is.EqualTo(86), "...and this one up");
        });
    }
}
