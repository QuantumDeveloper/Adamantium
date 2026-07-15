using System.Collections.Generic;
using System.Threading;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.UITests;

// The animations the RENDER thread plays by itself.
//
// The property that matters is also the one that is easy to fake, so it is the one these tests pin: an animation must advance
// WITHOUT the loop thread. No test here ticks AnimationManager to make something move - things move because TIME passes,
// which is exactly what happens while the loop thread sits inside a theme cascade.
[TestFixture]
public class CompositorTests
{
    private readonly List<Compositor.Entry> _view = new();

    [TearDown]
    public void TearDown() => AnimationManager.Reset();   // a static manager: leave nothing running for the next test

    private static (Border Element, Transform Transform) Spinner()
    {
        var element = new Border { Width = 40, Height = 40, RenderTransformOrigin = new Vector2(0.5, 0.5) };
        var transform = new Transform();
        element.RenderTransform = transform;

        element.Measure(new Size(100, 100));
        element.Arrange(new Rect(0, 0, 40, 40));
        return (element, transform);
    }

    private static Animation Spin() => new()
    {
        Duration = System.TimeSpan.FromSeconds(1),
        IterationCount = double.PositiveInfinity,
        KeyFrames =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter("RotationAngle", 0.0) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter("RotationAngle", 360.0) } }
        }
    };

    [Test]
    public void ASpinIsTakenOverByTheRenderThread()
    {
        var (element, transform) = Spinner();

        Spin().Apply(transform);

        var entry = Compositor.EntryFor(transform);
        Assert.That(entry, Is.Not.Null, "a pure transform animation must be composited");
        Assert.That(entry.Owner, Is.SameAs(element));
    }

    [Test]
    public void TakingOverPromotesTheElementToAMotionNode()
    {
        // A world-baked element cannot be moved by a matrix write: moving it would need a re-record, which is the loop
        // thread's job - and a stalled loop thread is precisely what this exists to survive.
        var (element, transform) = Spinner();
        Assert.That(element.IsRenderMotionNode, Is.False);

        Spin().Apply(transform);

        Assert.That(element.IsRenderMotionNode, Is.True);
    }

    [Test]
    public void ItAdvancesWithoutTheLoopThread()
    {
        var (_, transform) = Spinner();
        Spin().Apply(transform);

        Compositor.Tick(_view);
        var before = _view[0].Local;

        Thread.Sleep(60);   // the loop thread is "stuck": nobody ticks AnimationManager

        Compositor.Tick(_view);

        Assert.That(_view[0].Local, Is.Not.EqualTo(before), "the render thread must keep the spinner turning on its own");
    }

    [Test]
    public void TheLoopThreadMirrorsTheRenderThreadsClock()
    {
        // ONE clock, not two. When the loop thread comes back it must land on the value that is ON SCREEN - not resume from
        // where it left off, which would snap the element back by however long the stall lasted.
        var (_, transform) = Spinner();
        Spin().Apply(transform);

        Thread.Sleep(120);

        var entry = Compositor.EntryFor(transform);
        AnimationManager.Tick(0.0);          // a loop tick that brings NO time of its own
        var elapsed = entry.Elapsed;         // read right after: the mirror used a value no older than this

        var expected = elapsed % 1.0 * 360.0;   // a one-second spin, 0 -> 360
        Assert.That(transform.RotationAngle, Is.EqualTo(expected).Within(5.0),
            "the mirrored property must follow the compositor's clock, not one of its own");
        Assert.That(transform.RotationAngle, Is.GreaterThan(10.0),
            "and it must have moved at all - a loop tick with no delta would otherwise leave it at 0");
    }

    [Test]
    public void CancellingStopsTheRenderThreadToo()
    {
        var (_, transform) = Spinner();
        Spin().Apply(transform);
        Assert.That(Compositor.EntryFor(transform), Is.Not.Null);

        AnimationManager.Cancel(transform);

        Assert.That(Compositor.EntryFor(transform), Is.Null, "a stopped animation must not keep playing on screen");
        Assert.That(Compositor.Tick(_view), Is.False);
    }

    [Test]
    public void ALayoutAffectingAnimationIsNotComposited()
    {
        // Width needs layout, and layout belongs to the loop thread. Compositing it would draw a size nothing else agrees with.
        var element = new Border { Width = 40, Height = 40 };
        var grow = new Animation
        {
            Duration = System.TimeSpan.FromSeconds(1),
            KeyFrames =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter("Width", 40.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter("Width", 80.0) } }
            }
        };

        grow.Apply(element);

        Assert.That(Compositor.EntryFor(element), Is.Null);
        Assert.That(Compositor.Tick(_view), Is.False);
    }

    [Test]
    public void AnUnownedTransformIsNotComposited()
    {
        // A Transform that is nobody's RenderTransform moves nothing, so there is no element to promote and no matrix to
        // write. It stays on the loop thread rather than being silently dropped.
        var transform = new Transform();

        Spin().Apply(transform);

        Assert.That(Compositor.EntryFor(transform), Is.Null);

        AnimationManager.Tick(0.5);
        Assert.That(transform.RotationAngle, Is.EqualTo(180.0).Within(1.0), "and the loop thread still animates it");
    }

    // --- Paint channel: a brush's own opacity, on the render thread ---------------------------------------------------
    //
    // The skeleton pulse: ONE shared brush whose Opacity breathes while hundreds of cards paint with it. Unlike a transform,
    // it is NOT mirrored to the property system (colour touches neither layout nor hit-test) - the loop thread stops touching
    // it entirely, which is the whole point: it was the per-tick republish + mark-every-card that cost the most.

    private static SolidColorBrush PulsingBrush() => new(Colors.White) { Opacity = 0.05 };

    private static PulseAnimation Pulse() => new()
    {
        Property = "Opacity",
        Min = 0.05,
        Max = 0.15,
        Duration = System.TimeSpan.FromSeconds(1)
    };

    [Test]
    public void ABrushOpacityPulseIsTakenOverAsPaint()
    {
        var brush = PulsingBrush();

        Pulse().Apply(brush);

        var entry = Compositor.EntryFor(brush);
        Assert.That(entry, Is.Not.Null, "a brush-opacity pulse must be composited");
        Assert.That(entry.Channel, Is.EqualTo(CompositorChannel.Paint));
    }

    [Test]
    public void TheBrushPropertyIsNotMirrored_OnlyItsSnapshotAnimates()
    {
        var brush = PulsingBrush();
        brush.ForRendering();   // a drawn brush has a snapshot; the payloads do this
        Pulse().Apply(brush);

        Compositor.Tick(_view);
        var first = brush.Snapshot.Opacity;

        Thread.Sleep(120);   // the loop thread never ticks
        Compositor.Tick(_view);
        var later = brush.Snapshot.Opacity;

        Assert.That(later, Is.Not.EqualTo(first), "the render thread must keep the published snapshot breathing on its own");
        Assert.That(brush.Opacity, Is.EqualTo(0.05),
            "the LIVE property must stay at its authored base - paint is never mirrored, so nothing on the loop thread moved it");
        Assert.That(brush.Snapshot.Opacity, Is.InRange(0.05, 0.15), "and the snapshot stays within the pulse");
    }

    [Test]
    public void AnyDoubleBrushPropertyIsComposited_NotJustOpacity()
    {
        // The general case: RadialGradientBrush.RadiusX is a double AffectsPaint brush property, so it composites too - the
        // render thread applies it to the published snapshot generically (BuildAnimatedSnapshot sets ANY property on the
        // unfrozen clone). And, like every paint animation, it is NOT mirrored: the live property stays at its base.
        var brush = new RadialGradientBrush { RadiusX = 0.2 };
        brush.ForRendering();
        new Animation
        {
            Duration = System.TimeSpan.FromSeconds(1),
            IterationCount = double.PositiveInfinity,
            KeyFrames =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter("RadiusX", 0.2) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter("RadiusX", 0.8) } }
            }
        }.Apply(brush);

        Assert.That(Compositor.EntryFor(brush), Is.Not.Null, "a double paint property must be taken over");

        Compositor.Tick(_view);
        var first = ((RadialGradientBrush)brush.Snapshot).RadiusX;
        Thread.Sleep(120);
        Compositor.Tick(_view);
        var later = ((RadialGradientBrush)brush.Snapshot).RadiusX;

        Assert.That(later, Is.Not.EqualTo(first), "the render thread must animate the radius on the published snapshot");
        Assert.That(brush.RadiusX, Is.EqualTo(0.2), "and the live property is untouched - paint is not mirrored");
    }

    [Test]
    public void CancellingAPaintPulseStopsTheRenderThread()
    {
        var brush = PulsingBrush();
        Pulse().Apply(brush);
        Assert.That(Compositor.EntryFor(brush), Is.Not.Null);

        AnimationManager.Cancel(brush);

        Assert.That(Compositor.EntryFor(brush), Is.Null);
        Assert.That(Compositor.Tick(_view), Is.False);
    }

    [Test]
    public void ARecolourOfTheBaseFlowsThroughWhileAnimating()
    {
        // A theme swap recolours the shared brush mid-pulse. The base is re-captured on the loop thread (RefreshBases), so
        // the animated snapshot must pick up the new colour - the animation overrides only Opacity, never the colour.
        var brush = PulsingBrush();
        brush.ForRendering();
        Pulse().Apply(brush);
        Compositor.Tick(_view);
        Assert.That(((SolidColorBrush)brush.Snapshot).Color, Is.EqualTo(Colors.White));

        brush.Color = Colors.Red;      // the recolour, on the loop thread
        AnimationManager.Tick(0.0);    // a loop frame re-captures the base
        Compositor.Tick(_view);        // the render thread rebuilds the snapshot from it

        Assert.That(((SolidColorBrush)brush.Snapshot).Color, Is.EqualTo(Colors.Red),
            "the new base colour must reach the animated snapshot");
    }
}
