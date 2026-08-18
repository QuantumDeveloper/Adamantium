using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Media.Animation;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// An animation asked for while its element was NOT in the tree - a spinner started by a trigger inside a view built off
/// the loop thread - has nobody to re-ask for it later: a trigger's enter action runs once, as its condition becomes
/// true, and the resume path only re-runs what a DETACH suspended. So the request waits and is made when the element
/// goes up, and what these tests pin is that it then actually RUNS.
/// </summary>
[TestFixture]
public class DeferredAnimationTests
{
    private static Animation Spin()
    {
        var animation = new Animation { Duration = System.TimeSpan.FromSeconds(1), IterationCount = double.PositiveInfinity };
        var from = new KeyFrame { Cue = new Cue(0) };
        from.Setters.Add(new Adamantium.UI.Core.Resources.Setter(nameof(Border.Opacity), 0.0));
        var to = new KeyFrame { Cue = new Cue(1) };
        to.Setters.Add(new Adamantium.UI.Core.Resources.Setter(nameof(Border.Opacity), 1.0));
        animation.KeyFrames.Add(from);
        animation.KeyFrames.Add(to);
        return animation;
    }

    [TearDown]
    public void StopEverything() => AnimationManager.Reset();

    // The whole point, in one test: the animation is started on a thread that is materializing a subtree - so it must not
    // touch the heartbeat there - and once the element is in the tree it advances.
    [Test]
    public void AnimationAskedFor_WhileMaterializing_RunsOnceTheElementIsUp()
    {
        var border = new Border { Width = 20, Height = 20 };

        // Exactly what a trigger's enter action does, on the thread that builds the subtree - which reaches the element
        // before it is in any tree.
        Task.Run(() => Spin().Apply(border)).Wait();

        Assert.That(AnimationManager.ActiveCount, Is.Zero,
            "a subtree being built off the loop thread must not put anything on the heartbeat");

        var root = new StackPanel();
        root.Children.Add(border);
        var host = new Adamantium.UI.Controls.Window { Width = 100, Height = 100 };
        host.Content = root;
        ((Adamantium.UI.Core.IMeasurableComponent)host).Measure(new Size(100, 100));

        Assert.That(AnimationManager.ActiveCount, Is.GreaterThan(0),
            "the request waited for the element to go up - it must be running now");

        var before = border.Opacity;
        AnimationManager.Tick(0.3);
        Assert.That(border.Opacity, Is.Not.EqualTo(before), "...and running means advancing");
    }
}
