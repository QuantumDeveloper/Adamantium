using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>One in-flight keyframe <see cref="Animation"/>. It owns only a CLOCK and a target: the timing and interpolation
/// live in a pure <see cref="AnimationCurve"/>, so the same curve can be evaluated by the compositor on the render thread
/// while this one keeps the property system in step on the loop thread.</summary>
internal sealed class RunningKeyFrameAnimation : IRunningAnimation
{
    private readonly AdamantiumComponent _target;
    private readonly AnimationCurve _curve;
    private readonly Action _completed;
    private double _elapsedSeconds;

    // Set when the RENDER thread has taken this animation over. It then owns the clock, and this object's job shrinks to
    // MIRRORING: writing the composited value into the property system so hit-testing and bindings still agree with what is
    // on screen. It must not keep a clock of its own - two clocks on one animation is two answers to "where is it now".
    private readonly Compositor.Entry _composited;

    public RunningKeyFrameAnimation(AdamantiumComponent target, Animation animation, Action completed, double resumeElapsed = 0)
    {
        _target = target;
        _completed = completed;
        _elapsedSeconds = resumeElapsed;
        _curve = BuildCurve(target, animation);
        _composited = Compositor.TryTakeOver(target, _curve, resumeElapsed);
    }

    /// <summary>Where this animation is right now (composited: the render clock; else the local one) - what a re-templated
    /// successor resumes from so the motion doesn't snap back to the start.</summary>
    public double CurrentElapsed => _composited?.Elapsed ?? _elapsedSeconds;

    /// <summary>True when the render thread plays this animation and the loop thread only mirrors it.</summary>
    public bool IsComposited => _composited != null;

    /// <summary>The pure curve this animation plays - what the compositor takes over when it can.</summary>
    public AnimationCurve Curve => _curve;

    /// <summary>The properties this animation drives (used by the manager to drop conflicting in-flight animations).</summary>
    public IEnumerable<AdamantiumProperty> Properties => _curve.Tracks.Select(t => t.Property);

    public bool Animates(AdamantiumComponent target, AdamantiumProperty property) =>
        ReferenceEquals(_target, target) && _curve.Tracks.Any(t => t.Property == property);

    public bool AnimatesTarget(AdamantiumComponent target) => ReferenceEquals(_target, target);

    public IUIComponent DirtyTarget => _target as IUIComponent;

    public bool Advance(double deltaSeconds)
    {
        // Composited: the render thread's clock is THE clock. Read it rather than accumulating a second one - if the loop
        // thread was stalled for 300 ms, the animation on screen moved on by 300 ms, and anything the loop thread does with
        // this animation must agree with what the user is looking at, not lag it.
        _elapsedSeconds = _composited?.Elapsed ?? _elapsedSeconds + deltaSeconds;

        // Write the value into the property system UNLESS the render thread owns it AND applying it needs no property write.
        // A TRANSFORM is mirrored so hit-testing reads the animated matrix. A PAINT brush is not: colour touches neither
        // layout nor hit-test, and the whole point of compositing the skeleton pulse was to stop the loop thread republishing
        // one shared brush's snapshot and marking every card that paints with it dirty, every tick. So paint stays off the
        // loop thread entirely - the render thread applies it.
        if (_composited is not { Channel: CompositorChannel.Paint })
            foreach (var track in _curve.Tracks)
                _target.SetValue(track.Property, _curve.Evaluate(track, _elapsedSeconds), ValuePriority.Animation);

        if (!_curve.IsFinished(_elapsedSeconds)) return false;

        if (_composited != null) Compositor.Release(_target);
        _completed?.Invoke();
        return true;
    }

    private static AnimationCurve BuildCurve(AdamantiumComponent target, Animation animation)
    {
        // Collect every (cue, value) per property name across the keyframes.
        var byProperty = new Dictionary<string, List<(double cue, double value)>>();
        foreach (var keyFrame in animation.KeyFrames)
        {
            foreach (var setter in keyFrame.Setters.OfType<Setter>())
            {
                if (string.IsNullOrEmpty(setter.Property) || !TryToDouble(setter.Value, out var value))
                    continue;
                if (!byProperty.TryGetValue(setter.Property, out var stops))
                    byProperty[setter.Property] = stops = new List<(double, double)>();
                stops.Add((keyFrame.Cue.Value, value));
            }
        }

        var tracks = new List<AnimationCurve.Track>();
        foreach (var (name, stops) in byProperty)
        {
            var property = AdamantiumPropertyMap.FindRegistered(target.GetType(), name);
            if (property == null) continue;   // unknown / non-double property: skip (first cut interpolates doubles)

            var sorted = stops.OrderBy(s => s.cue).ToArray();
            tracks.Add(new AnimationCurve.Track(
                property,
                sorted.Select(s => s.cue).ToArray(),
                sorted.Select(s => s.value).ToArray()));
        }

        return new AnimationCurve([.. tracks], animation.Duration.TotalSeconds, animation.Delay.TotalSeconds,
            animation.IterationCount, animation.AutoReverse, animation.Easing);
    }

    private static bool TryToDouble(object value, out double result)
    {
        try
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
