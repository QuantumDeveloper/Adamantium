using Adamantium.UI.Core;
using Adamantium.UI.Core.Media.Animation;
using NUnit.Framework;

namespace Adamantium.UITests;

// The pure curve underneath every animation: elapsed seconds in, a value out. No target, no property write, no clock of its
// own - which is what lets two threads play the SAME animation without agreeing on anything but the time. These tests pin
// the timing rules (delay, iteration, auto-reverse, easing, clamping) so the compositor and the loop thread cannot drift.
[TestFixture]
public class AnimationCurveTests
{
    private static readonly AdamantiumProperty Prop = AdamantiumProperty.Register(
        "CurveTestValue", typeof(double), typeof(AnimationCurveTests), new PropertyMetadata(0.0));

    // 0 -> 100 over one second, unless stated otherwise.
    private static AnimationCurve Curve(double duration = 1.0, double delay = 0.0, double iterations = 1.0,
        bool autoReverse = false, IEasingFunction easing = null, double[] cues = null, double[] values = null)
        => new([new AnimationCurve.Track(Prop, cues ?? [0.0, 1.0], values ?? [0.0, 100.0])],
            duration, delay, iterations, autoReverse, easing);

    private static double At(AnimationCurve curve, double seconds) => curve.Evaluate(curve.Tracks[0], seconds);

    [Test]
    public void InterpolatesLinearlyAcrossTheDuration()
    {
        var curve = Curve();

        Assert.That(At(curve, 0.0), Is.EqualTo(0.0));
        Assert.That(At(curve, 0.25), Is.EqualTo(25.0).Within(1e-9));
        Assert.That(At(curve, 0.5), Is.EqualTo(50.0).Within(1e-9));
    }

    [Test]
    public void HoldsTheStartValueThroughTheDelay()
    {
        var curve = Curve(delay: 0.5);

        Assert.That(At(curve, 0.4), Is.EqualTo(0.0), "still waiting - the animation has not begun");
        Assert.That(At(curve, 1.0), Is.EqualTo(50.0).Within(1e-9), "half a second in, half way");
        Assert.That(curve.IsFinished(0.4), Is.False);
    }

    [Test]
    public void HoldsTheEndValueOnceFinished()
    {
        var curve = Curve();

        Assert.That(curve.IsFinished(0.99), Is.False);
        Assert.That(curve.IsFinished(1.0), Is.True);
        Assert.That(At(curve, 5.0), Is.EqualTo(100.0), "past the end it must not wrap back to the start");
    }

    [Test]
    public void RepeatsForTheGivenIterationCount()
    {
        var curve = Curve(iterations: 3);

        Assert.That(At(curve, 1.25), Is.EqualTo(25.0).Within(1e-9), "second iteration, a quarter in");
        Assert.That(At(curve, 2.5), Is.EqualTo(50.0).Within(1e-9), "third iteration, half way");
        Assert.That(curve.IsFinished(2.9), Is.False);
        Assert.That(curve.IsFinished(3.0), Is.True);
    }

    [Test]
    public void AnInfiniteAnimationNeverFinishes()
    {
        var curve = Curve(iterations: double.PositiveInfinity);

        Assert.That(curve.IsFinished(1_000_000.0), Is.False);
        Assert.That(At(curve, 10.5), Is.EqualTo(50.0).Within(1e-9), "and it keeps cycling");
    }

    [Test]
    public void AutoReverseRunsTheOddIterationsBackwards()
    {
        var curve = Curve(iterations: double.PositiveInfinity, autoReverse: true);

        Assert.That(At(curve, 0.25), Is.EqualTo(25.0).Within(1e-9), "first iteration: forwards");
        Assert.That(At(curve, 1.25), Is.EqualTo(75.0).Within(1e-9), "second iteration: backwards");
        Assert.That(At(curve, 2.25), Is.EqualTo(25.0).Within(1e-9), "third: forwards again");
    }

    [Test]
    public void EasingShapesThePosition()
    {
        var eased = Curve(easing: new CubicEasing { Mode = EasingMode.In });

        // Ease-in starts slow: at the half-way point it must be BELOW the linear 50.
        Assert.That(At(eased, 0.5), Is.LessThan(50.0));
        Assert.That(At(eased, 0.0), Is.EqualTo(0.0));
        Assert.That(At(eased, 1.0), Is.EqualTo(100.0).Within(1e-9), "and it still lands exactly on the end value");
    }

    [Test]
    public void EvaluatingIsPure_TheSameTimeAlwaysGivesTheSameValue()
    {
        // The property the compositor leans on: two threads evaluating the same curve at the same time agree, and
        // evaluating out of order (as a render thread reading ahead of the loop would) changes nothing.
        var curve = Curve(iterations: double.PositiveInfinity);

        var forwards = At(curve, 0.3);
        _ = At(curve, 7.9);
        Assert.That(At(curve, 0.3), Is.EqualTo(forwards));
    }

    [Test]
    public void MultipleTracksAdvanceOnOneClock()
    {
        // What makes a wave a wave: one curve, several properties, one time source.
        var other = AdamantiumProperty.Register("CurveTestOther", typeof(double), typeof(AnimationCurveTests),
            new PropertyMetadata(0.0));
        var curve = new AnimationCurve(
            [
                new AnimationCurve.Track(Prop, [0.0, 1.0], [0.0, 100.0]),
                new AnimationCurve.Track(other, [0.0, 1.0], [10.0, 20.0])
            ],
            1.0, 0.0, 1.0, false, null);

        Assert.That(curve.Evaluate(curve.Tracks[0], 0.5), Is.EqualTo(50.0).Within(1e-9));
        Assert.That(curve.Evaluate(curve.Tracks[1], 0.5), Is.EqualTo(15.0).Within(1e-9));
    }
}
