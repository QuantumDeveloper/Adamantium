using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using NUnit.Framework;
using Setter = Adamantium.UI.Core.Resources.Setter;

namespace Adamantium.UITests;

/// <summary>
/// What every loader in the theme actually is: a style trigger whose enter action spins a NAMED PART - and that part is a
/// <see cref="Transform"/>, not an element. Two things follow, and the Loaders page showed both when they were missed.
/// The action runs while the view is still being BUILT (a style is applied as the element is constructed, and an enter
/// action runs once, as its condition becomes true), so for content built off the loop thread it runs there - where the
/// tables it writes are not its to touch. And it cannot simply wait for "its target to attach", because a transform never
/// enters the visual tree at all: it has to wait on the element that OWNS it.
/// </summary>
[TestFixture]
public class DeferredLoaderAnimationTests
{
    // A trigger context of the shape the theme uses: the loader is the host, the named part it spins is a transform.
    private sealed class PartContext : ITriggerExecutionContext
    {
        private readonly IAdamantiumComponent _part;

        public PartContext(IFundamentalUIComponent host, IAdamantiumComponent part)
        {
            HostComponent = host;
            _part = part;
        }

        public IFundamentalUIComponent HostComponent { get; }
        public ITheme Theme => null;
        public IAdamantiumComponent FindTarget(string targetName) => _part;
    }

    private static RunAnimationAction Spin()
    {
        var animation = new Animation { Duration = TimeSpan.FromSeconds(1), IterationCount = double.PositiveInfinity };
        var start = new KeyFrame { Cue = new Cue(0) };
        start.Setters.Add(new Setter(nameof(Transform.RotationAngle), 0.0));
        var end = new KeyFrame { Cue = new Cue(1) };
        end.Setters.Add(new Setter(nameof(Transform.RotationAngle), 360.0));
        animation.KeyFrames.Add(start);
        animation.KeyFrames.Add(end);

        return new RunAnimationAction { TargetName = "RingSpin", Animation = animation };
    }

    // The loader spins FOREVER, which is the point of it - so it is taken off the heartbeat here instead of ticking on
    // through everyone else's tests.
    [TearDown]
    public void StopEverything() => AnimationManager.Reset();

    [Test]
    public void ALoaderStartedWhileItsViewWasBeingBuilt_SpinsOnceTheViewIsUp()
    {
        var spin = new Transform();
        var loader = new Border { Width = 20, Height = 20, RenderTransform = spin };
        var action = Spin();
        var running = AnimationManager.ActiveCount;   // the heartbeat is shared with whatever else is on it

        // Exactly what a style's enter action does, on the thread that materializes the subtree - the loader is not in
        // any tree yet.
        Task.Run(() => action.Invoke(new PartContext(loader, spin))).Wait();

        Assert.That(AnimationManager.ActiveCount, Is.EqualTo(running),
            "a subtree being built off the loop thread must put nothing on the heartbeat");

        new Rendering.TestRoot(100, 100).Add(loader);

        Assert.That(AnimationManager.ActiveCount, Is.GreaterThan(running),
            "the request waited on the element that owns the transform - being up, the loader must be running");

        var before = spin.RotationAngle;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 2 && Math.Abs(spin.RotationAngle - before) < 0.001)
        {
            AnimationManager.Tick(0.05);
            Thread.Sleep(1);
        }

        Assert.That(Math.Abs(spin.RotationAngle - before), Is.GreaterThan(0.001),
            "...and running means the part actually turns");
    }
}
