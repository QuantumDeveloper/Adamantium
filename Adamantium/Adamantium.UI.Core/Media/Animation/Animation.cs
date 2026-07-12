using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// A keyframe animation (CSS <c>@keyframes</c> analog, own implementation): interpolates the double properties named in
/// its <see cref="KeyFrames"/>' setters through those keyframes over <see cref="Duration"/>, honouring
/// <see cref="Delay"/>, <see cref="IterationCount"/> (<see cref="double.PositiveInfinity"/> = loop) and
/// <see cref="AutoReverse"/> (ping-pong). Start it with <see cref="Apply"/>.
/// </summary>
public class Animation
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Wait this long before the first iteration (start values held during the delay).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>How many iterations to play; <see cref="double.PositiveInfinity"/> loops forever. Default 1.</summary>
    public double IterationCount { get; set; } = 1;

    /// <summary>When true, every other iteration plays backwards (ping-pong).</summary>
    public bool AutoReverse { get; set; }

    /// <summary>Easing applied across the iteration's timeline; null means linear.</summary>
    public IEasingFunction Easing { get; set; }

    /// <summary>The keyframes, by <see cref="KeyFrame.Cue"/> (order is normalised at run time). Markup content, so
    /// they are written directly inside &lt;Animation&gt;.</summary>
    [Content]
    public List<KeyFrame> KeyFrames { get; } = new();

    /// <summary>Starts this animation on <paramref name="target"/>; <paramref name="completed"/> fires when it ends
    /// (never, for an infinite animation). The target is any <see cref="AdamantiumComponent"/> - a UI element OR a
    /// non-visual component such as a <c>GradientStop</c> (whose owning element repaints via its brush subscription).</summary>
    public void Apply(AdamantiumComponent target, Action completed = null)
    {
        Prepare();
        AnimationManager.BeginKeyFrame(target, this, completed);
    }

    /// <summary>Hook for a prebuilt animation TYPE (e.g. <c>PulseAnimation</c>) to synthesize its <see cref="KeyFrames"/>
    /// from friendly properties (Min/Max/...) just before it runs. The base keyframe animation authors its frames in
    /// markup, so this is a no-op for it.</summary>
    protected virtual void Prepare() { }
}
