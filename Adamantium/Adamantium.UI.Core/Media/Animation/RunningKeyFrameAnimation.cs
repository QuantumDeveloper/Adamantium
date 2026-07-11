using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>One in-flight keyframe <see cref="Animation"/>. Resolves each animated property's keyframe stops to a sorted
/// (cue, double) track once, then each frame computes the iteration position (honouring Delay/IterationCount/AutoReverse
/// + easing) and writes each track's interpolated value at <see cref="ValuePriority.Animation"/>.</summary>
internal sealed class RunningKeyFrameAnimation : IRunningAnimation
{
    private sealed class Track
    {
        public AdamantiumProperty Property;
        public double[] Cues;
        public double[] Values;
    }

    private readonly AnimatableUIComponent _target;
    private readonly double _durationSeconds;
    private readonly double _delaySeconds;
    private readonly double _iterationCount;
    private readonly bool _autoReverse;
    private readonly IEasingFunction _easing;
    private readonly Action _completed;
    private readonly List<Track> _tracks;
    private double _elapsedSeconds;

    public RunningKeyFrameAnimation(AnimatableUIComponent target, Animation animation, Action completed)
    {
        _target = target;
        _durationSeconds = Math.Max(0.0001, animation.Duration.TotalSeconds);
        _delaySeconds = Math.Max(0.0, animation.Delay.TotalSeconds);
        _iterationCount = animation.IterationCount;
        _autoReverse = animation.AutoReverse;
        _easing = animation.Easing;
        _completed = completed;
        _tracks = BuildTracks(target, animation);
    }

    /// <summary>The properties this animation drives (used by the manager to drop conflicting in-flight animations).</summary>
    public IEnumerable<AdamantiumProperty> Properties => _tracks.Select(t => t.Property);

    public bool Animates(AdamantiumComponent target, AdamantiumProperty property) =>
        ReferenceEquals(_target, target) && _tracks.Any(t => t.Property == property);

    public IUIComponent DirtyTarget => _target as IUIComponent;

    public bool Advance(double deltaSeconds)
    {
        _elapsedSeconds += deltaSeconds;
        var active = _elapsedSeconds - _delaySeconds;

        double progress;
        var finished = false;
        if (active <= 0.0)
        {
            progress = 0.0;   // still in the delay: hold the start values
        }
        else
        {
            var cycles = active / _durationSeconds;
            if (!double.IsPositiveInfinity(_iterationCount) && cycles >= _iterationCount)
            {
                progress = _iterationCount;
                finished = true;
            }
            else
            {
                progress = cycles;
            }
        }

        var position = CyclePosition(progress);
        foreach (var track in _tracks)
            _target.SetValue(track.Property, Interpolate(track, position), ValuePriority.Animation);

        if (finished)
        {
            _completed?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>The eased position within the current iteration, 0..1. Integer boundaries are the END of the previous
    /// iteration; with <see cref="_autoReverse"/> odd iterations run backwards.</summary>
    private double CyclePosition(double progress)
    {
        var iteration = Math.Floor(progress);
        var localT = progress - iteration;
        if (localT == 0.0 && progress > 0.0)
        {
            iteration -= 1;
            localT = 1.0;
        }

        var reversed = _autoReverse && (long)iteration % 2 == 1;
        var pos = reversed ? 1.0 - localT : localT;
        return _easing?.Ease(pos) ?? pos;
    }

    private static double Interpolate(Track track, double position)
    {
        var cues = track.Cues;
        var values = track.Values;
        if (position <= cues[0]) return values[0];
        if (position >= cues[^1]) return values[^1];

        for (var i = 0; i < cues.Length - 1; i++)
        {
            if (position <= cues[i + 1])
            {
                var span = cues[i + 1] - cues[i];
                var t = span <= 0 ? 0.0 : (position - cues[i]) / span;
                return values[i] + (values[i + 1] - values[i]) * t;
            }
        }
        return values[^1];
    }

    private static List<Track> BuildTracks(AnimatableUIComponent target, Animation animation)
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

        var tracks = new List<Track>();
        foreach (var (name, stops) in byProperty)
        {
            var property = AdamantiumPropertyMap.FindRegistered(target.GetType(), name);
            if (property == null) continue;   // unknown / non-double property: skip (first cut interpolates doubles)

            var sorted = stops.OrderBy(s => s.cue).ToArray();
            tracks.Add(new Track
            {
                Property = property,
                Cues = sorted.Select(s => s.cue).ToArray(),
                Values = sorted.Select(s => s.value).ToArray(),
            });
        }
        return tracks;
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
