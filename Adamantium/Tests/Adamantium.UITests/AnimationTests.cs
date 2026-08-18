using System;
using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class AnimationTests
{
    // The heartbeat runs what is ON SCREEN: an element outside the tree is not animated, because there is nothing there to
    // move. So a host in these tests goes up the same way a real one does.
    private static Border OnScreen()
    {
        var border = new Border();
        new Rendering.TestRoot(100, 100).Add(border);
        return border;
    }

    [Test]
    public void DoubleAnimation_InterpolatesAndCompletes()
    {
        var transform = new Transform();
        var completed = false;

        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 100, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() },
            () => completed = true);

        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "From is applied immediately");

        AnimationManager.Tick(0.5);
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(0.5), "halfway, linear");
        Assert.That(completed, Is.False);

        AnimationManager.Tick(0.6);   // total 1.1s -> finished
        Assert.That(transform.TranslateX, Is.EqualTo(100).Within(0.001), "final value held");
        Assert.That(completed, Is.True);
    }

    [Test]
    public void DoubleAnimation_RestartReplacesInFlight()
    {
        var transform = new Transform();
        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 100, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() });
        AnimationManager.Tick(0.5);   // ~50

        // Re-target the same property: the in-flight animation is dropped and a fresh one starts from its From.
        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 200, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() });
        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "restarted from the new From");

        AnimationManager.Tick(1.0);
        Assert.That(transform.TranslateX, Is.EqualTo(200).Within(0.001));
    }

    [Test]
    public void CancelAnimation_StopsAndRevertsToBaseValue()
    {
        var transform = new Transform();
        transform.TranslateX = 10;   // local base value

        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 100, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() });
        AnimationManager.Tick(0.5);
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(0.5), "animation overrides the local value while running");

        transform.CancelAnimation(Transform.TranslateXProperty);
        Assert.That(transform.TranslateX, Is.EqualTo(10).Within(0.001), "cancel releases the animation -> reverts to local base");

        AnimationManager.Tick(0.5);   // further ticks must not move it
        Assert.That(transform.TranslateX, Is.EqualTo(10).Within(0.001), "no longer animating after cancel");
    }

    [Test]
    public void Transition_AnimatesBaseValueChangeFromCurrentToNew()
    {
        var transform = new Transform
        {
            Transitions = new Transitions
            {
                new DoubleTransition { Property = "TranslateX", Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() }
            }
        };

        transform.TranslateX = 100;   // base change 0 -> 100: the transition eases instead of snapping
        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "starts from the current value (0), not the target");

        AnimationManager.Tick(0.5);
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(1.0), "halfway");

        AnimationManager.Tick(0.6);
        Assert.That(transform.TranslateX, Is.EqualTo(100).Within(0.001), "settles at the new base value");
    }

    [Test]
    public void DoubleAnimation_AutoReverse_PingPongsAndEndsAtFrom()
    {
        var transform = new Transform();
        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 100, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing(),
                                  IterationCount = 2, AutoReverse = true });

        AnimationManager.Tick(0.5);    // iteration 0 forward @0.5 -> 50
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(1.0), "first iteration plays forward");

        AnimationManager.Tick(0.75);   // total 1.25 -> iteration 1 (reversed) @0.25 -> 75
        Assert.That(transform.TranslateX, Is.EqualTo(75).Within(1.0), "second iteration plays backwards");

        AnimationManager.Tick(1.0);    // total 2.25 >= 2 iterations -> finished at From (even ping-pong count)
        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "ends back at From");
    }

    [Test]
    public void DoubleAnimation_Delay_HoldsFromThenAnimates()
    {
        var transform = new Transform();
        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 100, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing(),
                                  Delay = TimeSpan.FromSeconds(0.5) });

        AnimationManager.Tick(0.4);    // within the delay -> holds From
        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "holds From during the delay");

        AnimationManager.Tick(0.6);    // total 1.0 -> active 0.5 -> 50
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(1.0), "animates after the delay");
    }

    [Test]
    public void DoubleAnimation_InfiniteIteration_NeverCompletes()
    {
        var transform = new Transform();
        var completed = false;
        transform.BeginAnimation(Transform.TranslateXProperty,
            new DoubleAnimation { From = 0, To = 100, Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing(),
                                  IterationCount = double.PositiveInfinity },
            () => completed = true);

        AnimationManager.Tick(10.5);   // many iterations later
        Assert.That(completed, Is.False, "an infinite animation never fires completion");
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(1.0), "still cycling (10.5 -> iteration 10 @0.5)");

        transform.CancelAnimation(Transform.TranslateXProperty);   // stop it so it doesn't advance in later tests
    }

    [Test]
    public void Animation_KeyFrames_InterpolateThroughStops()
    {
        var transform = new Transform();
        var anim = new Animation { Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() };
        anim.KeyFrames.Add(KeyFrameAt(0.0, 0.0));     // 0%   -> 0
        anim.KeyFrames.Add(KeyFrameAt(0.5, 100.0));   // 50%  -> 100
        anim.KeyFrames.Add(KeyFrameAt(1.0, 0.0));     // 100% -> 0
        anim.Apply(transform);

        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "starts at the first keyframe");

        AnimationManager.Tick(0.25);   // between 0% and 50% -> 50
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(1.0), "rises toward the middle keyframe");

        AnimationManager.Tick(0.25);   // 50% -> 100
        Assert.That(transform.TranslateX, Is.EqualTo(100).Within(1.0), "peaks at the middle keyframe");

        AnimationManager.Tick(0.25);   // between 50% and 100% -> 50
        Assert.That(transform.TranslateX, Is.EqualTo(50).Within(1.0), "falls back toward the last keyframe");

        AnimationManager.Tick(0.5);    // past the end -> last keyframe
        Assert.That(transform.TranslateX, Is.EqualTo(0).Within(0.001), "ends at the last keyframe");
    }

    private static KeyFrame KeyFrameAt(double cue, double translateX)
    {
        var keyFrame = new KeyFrame { Cue = cue };
        keyFrame.Setters.Add(new Setter("TranslateX", translateX));
        return keyFrame;
    }

    [Test]
    public void PropertyTrigger_EnterAction_RunsAnimation()
    {
        var border = OnScreen();

        var trigger = new PropertyTrigger { Property = "Width", Value = 200.0 };
        var anim = new Animation { Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() };
        anim.KeyFrames.Add(KeyFrameWith("Height", 0.0, 0.0));
        anim.KeyFrames.Add(KeyFrameWith("Height", 1.0, 100.0));
        trigger.EnterActions.Add(new RunAnimationAction { Animation = anim });

        trigger.Apply(new SelfTriggerContext(border));   // condition not met yet (Width = NaN)

        border.Width = 200;   // condition becomes true -> EnterAction starts the animation
        AnimationManager.Tick(0.5);
        Assert.That(border.Height, Is.EqualTo(50).Within(1.0), "trigger enter action runs the animation");
    }

    [Test]
    public void CueParser_ParsesFractionAndPercent()
    {
        Assert.That(TypeParser.Parse<Cue>("0.5").Value, Is.EqualTo(0.5).Within(1e-9));
        Assert.That(TypeParser.Parse<Cue>("50%").Value, Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void EasingParser_ParsesNamedEasings()
    {
        // Goes through TypeParser, so it also proves the [TypeParser] attribute on IEasingFunction is wired (same path
        // codegen + the runtime loader use for Easing="...").
        Assert.That(TypeParser.Parse<IEasingFunction>("Linear"), Is.InstanceOf<LinearEasing>());

        var cubicOut = TypeParser.Parse<IEasingFunction>("CubicOut");
        Assert.That(cubicOut, Is.InstanceOf<CubicEasing>());
        Assert.That(((CubicEasing)cubicOut).Mode, Is.EqualTo(EasingMode.Out));
    }

    [Test]
    public void SharedAnimationTarget_RunsOnce_AndStopsOnlyWhenTheLastHostReleases()
    {
        // The loading-skeleton pulse: EVERY loading list animates the SAME keyed theme brush (that is what makes a
        // screenful of cards cost one animation, not one per card). So two hosts running the same animation on it must
        // collapse to ONE, and the first host to stop must not kill the pulse the other still wants.
        AnimationManager.Reset();
        var sharedBrush = new SolidColorBrush(Colors.White);
        var run = new RunAnimationAction { TargetName = "SkeletonPulseFill", Animation = PulseOpacity() };
        var stop = new StopAnimationAction { TargetName = "SkeletonPulseFill" };
        var listA = new KeyedTargetContext(OnScreen(), sharedBrush);
        var listB = new KeyedTargetContext(OnScreen(), sharedBrush);

        run.Invoke(listA);
        run.Invoke(listB);
        Assert.That(AnimationManager.ActiveCount, Is.EqualTo(1), "one animation on the shared target, not one per host");

        stop.Invoke(listA);
        Assert.That(AnimationManager.ActiveCount, Is.EqualTo(1), "the other host still wants it - keep pulsing");

        stop.Invoke(listB);
        Assert.That(AnimationManager.ActiveCount, Is.EqualTo(0), "last host released -> the pulse stops");
    }

    [Test]
    public void TriggerDeactivatedWhileActive_StopsTheAnimationItStarted()
    {
        // A theme/template swap deactivates a trigger whose condition still HOLDS, so its exit edge never comes. A
        // LOOPING animation started by the enter action would then tick forever against a brush nobody paints with.
        AnimationManager.Reset();
        var sharedBrush = new SolidColorBrush(Colors.White);
        var host = OnScreen();

        var trigger = new PropertyTrigger { Property = "Width", Value = 200.0 };
        trigger.EnterActions.Add(new RunAnimationAction { TargetName = "SkeletonPulseFill", Animation = PulseOpacity() });
        var activator = trigger.Apply(new KeyedTargetContext(host, sharedBrush));

        host.Width = 200;   // condition met -> the looping pulse starts
        Assert.That(AnimationManager.ActiveCount, Is.EqualTo(1));

        activator.Deactivate();
        Assert.That(AnimationManager.ActiveCount, Is.EqualTo(0), "teardown must not leave a looping animation behind");
    }

    private static PulseAnimation PulseOpacity() =>
        new() { Property = "Opacity", Min = 0.05, Max = 0.15, Duration = TimeSpan.FromSeconds(1) };

    private static KeyFrame KeyFrameWith(string property, double cue, double value)
    {
        var keyFrame = new KeyFrame { Cue = cue };
        keyFrame.Setters.Add(new Setter(property, value));
        return keyFrame;
    }

    // Minimal style/logical execution context (StyleTriggerExecutionContext is internal): the host is the only target.
    private sealed class SelfTriggerContext : ITriggerExecutionContext
    {
        private readonly IFundamentalUIComponent _host;
        public SelfTriggerContext(IFundamentalUIComponent host) => _host = host;
        public IFundamentalUIComponent HostComponent => _host;
        public ITheme Theme => null;
        public IAdamantiumComponent FindTarget(string targetName) => _host;
    }

    // A named target that is NOT a part of the host - what the real context resolves from the theme's keyed resources
    // (a shared brush). Each context is one HOST animating that same shared target.
    private sealed class KeyedTargetContext : ITriggerExecutionContext
    {
        private readonly IAdamantiumComponent _target;
        public KeyedTargetContext(IFundamentalUIComponent host, IAdamantiumComponent target)
        {
            HostComponent = host;
            _target = target;
        }
        public IFundamentalUIComponent HostComponent { get; }
        public ITheme Theme => null;
        public IAdamantiumComponent FindTarget(string targetName) =>
            string.IsNullOrEmpty(targetName) ? HostComponent : _target;
    }

    [Test]
    public void WorldTransform_HonorsRenderTransform()
    {
        var element = new Border();
        var baseline = element.WorldTransform;

        element.RenderTransform = new Transform { TranslateX = 30, TranslateY = 12 };
        var moved = element.WorldTransform;

        // Translation lives in M41/M42; comparing the delta isolates the RenderTransform contribution from any bounds.
        Assert.That(moved.M41 - baseline.M41, Is.EqualTo(30).Within(0.001), "RenderTransform.TranslateX feeds WorldTransform");
        Assert.That(moved.M42 - baseline.M42, Is.EqualTo(12).Within(0.001), "RenderTransform.TranslateY feeds WorldTransform");
    }

    [Test]
    public void ContentTransition_KeepsOutgoingThenRemovesOnCompletion()
    {
        var presenter = new ContentPresenter
        {
            ContentTransition = ContentTransition.SlideLeft,
            TransitionDuration = 0.2,
        };

        var first = new Border();
        presenter.Content = first;
        presenter.Measure(new Size(200, 100), force: true);
        presenter.Arrange(new Rect(0, 0, 200, 100));

        var second = new Border();
        presenter.Content = second;
        presenter.Measure(new Size(200, 100), force: true);
        presenter.Arrange(new Rect(0, 0, 200, 100));

        Assert.That(presenter.GetVisualDescendants(), Does.Contain(first), "outgoing content kept during the slide");
        Assert.That(presenter.GetVisualDescendants(), Does.Contain(second), "incoming content added");
        Assert.That(second.RenderTransform, Is.Not.Null);
        Assert.That(second.RenderTransform.TranslateX, Is.EqualTo(200).Within(0.5), "incoming starts off-screen at +width");

        AnimationManager.Tick(0.25);   // past the 0.2s duration

        Assert.That(second.RenderTransform.TranslateX, Is.EqualTo(0).Within(0.01), "incoming settles at 0");
        Assert.That(presenter.GetVisualDescendants(), Does.Not.Contain(first), "outgoing removed once its slide-out completes");
    }
}
