using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media.Animation;
using NUnit.Framework;

namespace Adamantium.UITests;

// Inertial scrolling: the wheel path eases to its target over frames (not an instant jump) and settles, driven by the
// AnimationManager heartbeat. (Flick momentum shares the same driver; its feel is verified in the running app.)
public class InertiaScrollTests
{
    private static ScrollContentPresenter MakeScroller()
    {
        var scp = new ScrollContentPresenter { Content = new Border { Height = 1000 } };
        scp.Measure(new Size(100, 300));   // content extent 1000, viewport 300 -> scrollable
        scp.Arrange(new Rect(0, 0, 100, 300));
        return scp;
    }

    [Test]
    public void WheelScroll_IsSmooth_NotInstant_AndSettles()
    {
        AnimationManager.Reset();
        var scp = MakeScroller();

        scp.AnimateScrollBy(new Vector2(0, 200));
        Assert.That(scp.Offset.Y, Is.EqualTo(0).Within(0.01), "a smooth scroll must not jump instantly - it eases over frames");

        for (var i = 0; i < 120 && AnimationManager.HasActiveAnimations; i++)
            AnimationManager.Tick(1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(scp.Offset.Y, Is.EqualTo(200).Within(1.0), "the eased scroll reaches its target");
            Assert.That(AnimationManager.HasActiveAnimations, Is.False, "the inertia ticker stops once settled");
        });
    }

    [Test]
    public void WheelScroll_InstantWhenInertiaDisabled()
    {
        AnimationManager.Reset();
        var scp = MakeScroller();
        scp.IsInertiaEnabled = false;

        scp.AnimateScrollBy(new Vector2(0, 150));
        Assert.That(scp.Offset.Y, Is.EqualTo(150).Within(0.5), "with inertia off, the scroll applies instantly");
        Assert.That(AnimationManager.HasActiveAnimations, Is.False, "no ticker is registered when inertia is off");
    }
}
