using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class AnimationTests
{
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
