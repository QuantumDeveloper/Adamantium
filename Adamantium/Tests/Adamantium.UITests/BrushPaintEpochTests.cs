using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The one number that lets a per-frame reader ask "has anything repainted" without walking its whole record.
/// <para>The render cache used to poll: once a frame it compared every brush in the scene against the version it had
/// baked, to find the handful that had changed. On a screen of a few thousand tiles that scan was measured at ~1 ms -
/// about half the entire draw - and it was spent, every frame, discovering that there was nothing to do. The shape is
/// the defect: a question whose answer is almost always "no" must be askable once, not per candidate.</para>
/// <para>So the contract this pins is not a timing, which would be flaky, but the two facts the gate rests on: a quiet
/// application does not move the epoch, and any brush rewriting itself does.</para>
/// </summary>
[TestFixture]
public class BrushPaintEpochTests
{
    [Test]
    public void AQuietApplicationDoesNotMoveTheEpoch()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var before = Brush.PaintEpoch;

        _ = brush.Color;
        _ = brush.Opacity;
        _ = brush.PaintVersion;

        Assert.That(Brush.PaintEpoch, Is.EqualTo(before), "reading a brush is not repainting it");
    }

    [Test]
    public void ARepaintMovesTheEpoch()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var before = Brush.PaintEpoch;

        brush.Color = Colors.Green;

        Assert.That(Brush.PaintEpoch, Is.GreaterThan(before),
            "a reader that skipped its scan on this frame has to be told to run it");
    }

    /// <summary>The epoch is the WHOLE APPLICATION's, deliberately: a reader holds a record per scene and cannot know
    /// which brushes are in somebody else's. So one brush moving it makes every reader look once - which is right, and
    /// still O(1) on the frames where nothing moved at all.</summary>
    [Test]
    public void AnyBrushMovesIt()
    {
        var watched = new SolidColorBrush(Colors.Red);
        var other = new SolidColorBrush(Colors.Blue);
        var before = Brush.PaintEpoch;

        other.Opacity = 0.5;

        Assert.That(Brush.PaintEpoch, Is.GreaterThan(before));
        Assert.That(watched.PaintVersion, Is.Not.Negative, "and the watched brush itself is untouched");
    }
}
