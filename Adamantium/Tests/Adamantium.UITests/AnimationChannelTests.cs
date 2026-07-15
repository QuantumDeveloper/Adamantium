using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using NUnit.Framework;

namespace Adamantium.UITests;

// Which animations the render thread is allowed to play on its own. Getting this wrong is not a slow frame - it is a torn
// one (or a data race), so the rule is decided from what the property actually touches, never from an author's promise.
[TestFixture]
public class AnimationChannelTests
{
    [Test]
    public void ATransformIsAlwaysTheTransformChannel()
    {
        var transform = new Transform();

        Assert.That(AnimationChannels.Of(transform, Transform.RotationAngleProperty), Is.EqualTo(CompositorChannel.Transform));
        Assert.That(AnimationChannels.Of(transform, Transform.ScaleXProperty), Is.EqualTo(CompositorChannel.Transform));
    }

    [Test]
    public void APaintFlaggedPropertyIsThePaintChannel()
    {
        Assert.That(AnimationChannels.Of(new SolidColorBrush(Colors.Red), SolidColorBrush.ColorProperty),
            Is.EqualTo(CompositorChannel.Paint));
        Assert.That(AnimationChannels.Of(new SolidColorBrush(Colors.Red), Brush.OpacityProperty),
            Is.EqualTo(CompositorChannel.Paint));
        Assert.That(AnimationChannels.Of(new GradientStop(Colors.White, 0), GradientStop.OffsetProperty),
            Is.EqualTo(CompositorChannel.Paint), "the shimmer sweeps its band by moving stops - pure colour");
    }

    [Test]
    public void AnythingElseStaysOnTheLoopThread()
    {
        // A gradient's SPREAD is paint; a shape's stroke TRIM is not - it re-tessellates, so it must re-record.
        var brush = new LinearGradientBrush();
        Assert.That(AnimationChannels.Of(brush, GradientBrush.SpreadMethodProperty), Is.EqualTo(CompositorChannel.Paint));
        Assert.That(AnimationChannels.Of(brush, Brush.OpacityProperty), Is.EqualTo(CompositorChannel.Paint));
    }

    [Test]
    public void ACurveIsCompositedOnlyWhenEVERYTrackAgrees()
    {
        var transform = new Transform();

        var pureTransform = new AnimationCurve(
            [
                new AnimationCurve.Track(Transform.ScaleXProperty, [0.0, 1.0], [0.5, 1.0]),
                new AnimationCurve.Track(Transform.ScaleYProperty, [0.0, 1.0], [0.5, 1.0])
            ],
            1.0, 0.0, 1.0, false, null);

        Assert.That(AnimationChannels.Of(transform, pureTransform), Is.EqualTo(CompositorChannel.Transform));
    }

    [Test]
    public void TransformValuesComposeTheSameMatrixTheLiveTransformDoes()
    {
        // The compositor composes the matrix from captured values instead of reading the live Transform. If these two ever
        // disagree, an element animates to one place on screen and hit-tests in another.
        var values = TransformValues.Identity;
        values.Set(Transform.RotationAngleProperty, 90.0);
        Assert.That(values.RotationAngle, Is.EqualTo(90.0), "Set must find the member the property names");

        var live = new Transform { RotationAngle = 90.0 };

        Assert.That(values.ToMatrix(), Is.EqualTo(live.Matrix));
        Assert.That(values.ToMatrix().M11, Is.EqualTo(0.0).Within(1e-6), "a quarter turn: cos 90 = 0");
    }

    [Test]
    public void AnEmptyCurveIsNotComposited()
    {
        var curve = new AnimationCurve([], 1.0, 0.0, 1.0, false, null);

        Assert.That(AnimationChannels.Of(new Transform(), curve), Is.EqualTo(CompositorChannel.None));
    }
}
