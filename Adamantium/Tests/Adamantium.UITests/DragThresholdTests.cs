using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The OS states the drag threshold in PHYSICAL pixels, and a control measures its delta in its OWN units.
/// Everything between the two - the window's DPI, a ZoomBox, a designer zoom - is a factor, and comparing the numbers
/// without it hands the user a setting they did not choose.</summary>
[TestFixture]
public class DragThresholdTests
{
    private sealed class Settings : INativePlatformSettings
    {
        public uint DoubleClickTime => 500;

        public Size DragThreshold => new(4, 4);

        public uint HoverTime => 400;

        public Rect VirtualScreen => default;
    }

    private INativePlatformSettings _previous;

    [SetUp]
    public void SetUp()
    {
        _previous = PlatformSettings.Platform;
        PlatformSettings.Platform = new Settings();
    }

    [TearDown]
    public void TearDown() => PlatformSettings.Platform = _previous;

    private static bool Exceeds(double delta, double scale)
        => PlatformSettings.ExceedsDragThreshold(new Vector2((float)delta, 0), new Vector2((float)scale, (float)scale));

    [Test]
    public void AtOneToOneItIsTheOsSettingExactly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Exceeds(4, 1.0), Is.False, "AT the threshold is not past it");
            Assert.That(Exceeds(4.1, 1.0), Is.True);
        });
    }

    // The regression this exists for. At 150% one unit is 1.5 physical px, so the user's 4px setting is crossed at 2.67
    // units - and comparing units against the physical number instead made them travel 4 units, which is 6 physical px:
    // half again as far as they asked for. The same factor stacks with a zoomed subtree.
    [Test]
    public void AScaledDisplayDoesNotMoveTheUsersSetting()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Exceeds(2.6, 1.5), Is.False, "3.9 physical px - not there yet");
            Assert.That(Exceeds(2.7, 1.5), Is.True, "4.05 physical px - past the 4px setting");
            Assert.That(Exceeds(4, 1.5), Is.True, "and 6 physical px is long past it, where this used to be the boundary");
        });
    }

    // A ZoomBox is the same arithmetic from the other side, and it goes BOTH ways: magnified, the old comparison would
    // not let go of a click until the hand had crossed the threshold twice over; reduced, it turned a click into a drag.
    [Test]
    public void AZoomedSubtreeCountsToo()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Exceeds(2.1, 2.0), Is.True, "4.2 physical px inside a 2x zoom");
            Assert.That(Exceeds(4, 0.5), Is.False, "2 physical px inside a half-scale subtree is not a drag");
            Assert.That(Exceeds(8.1, 0.5), Is.True);
        });
    }

    // Per axis, not radial - what the OS setting means, and what every other application does with it.
    [Test]
    public void EitherAxisOnItsOwnIsEnough()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlatformSettings.ExceedsDragThreshold(new Vector2(0, 5), Vector2.One), Is.True);
            Assert.That(PlatformSettings.ExceedsDragThreshold(new Vector2(3, 3), Vector2.One), Is.False,
                "3,3 is 4.2 away radially and still not a drag");
        });
    }

    // A desktop distance is physical on both sides and has nothing to convert.
    [Test]
    public void ADesktopDistanceNeedsNoScale()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlatformSettings.ExceedsDragThreshold(new PixelPoint(4, 0)), Is.False);
            Assert.That(PlatformSettings.ExceedsDragThreshold(new PixelPoint(5, 0)), Is.True);
        });
    }

    // An element in no window has no screen to measure against, and 1,1 is the honest answer - not a refusal, which
    // would make every gesture in an off-screen bake or a detached tree a drag from the first pixel.
    [Test]
    public void AnElementWithNoWindowCountsOneToOne()
    {
        Assert.That(PlatformSettings.PhysicalPerUnit(null), Is.EqualTo(Vector2.One));
    }
}
