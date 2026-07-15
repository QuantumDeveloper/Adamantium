using System;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// A PURE animation curve: elapsed seconds in, a value per track out. It holds no target, writes no property and carries no
/// mutable state, so ANY thread may evaluate it at ANY time, as often as it likes.
/// </summary>
/// <remarks>
/// That is the whole basis of the compositor. An animation that lives inside <c>SetValue</c> can only advance as fast as the
/// thread that owns the property system - so while the loop thread is busy (a theme cascade re-templating the tree), every
/// spinner on screen freezes precisely when the user most needs to see one. With the timing separated from the writing, the
/// render thread evaluates the curve on ITS clock and presents a smooth frame regardless, and the loop thread evaluates the
/// SAME curve on demand (a hit-test, a binding read) - so the tree never disagrees with the screen about where a spinning
/// element is.
///
/// Immutable by construction: the arrays are built once (see <see cref="RunningKeyFrameAnimation"/>) and never handed out.
/// </remarks>
public sealed class AnimationCurve
{
    /// <summary>One animated property and its keyframe stops, sorted by cue. Cues are 0..1 within one iteration.</summary>
    public sealed class Track
    {
        public Track(AdamantiumProperty property, double[] cues, double[] values)
        {
            Property = property;
            Cues = cues;
            Values = values;
        }

        public AdamantiumProperty Property { get; }
        internal double[] Cues { get; }
        internal double[] Values { get; }
    }

    private readonly double _durationSeconds;
    private readonly double _delaySeconds;
    private readonly double _iterationCount;
    private readonly bool _autoReverse;
    private readonly IEasingFunction _easing;

    public AnimationCurve(Track[] tracks, double durationSeconds, double delaySeconds, double iterationCount,
        bool autoReverse, IEasingFunction easing)
    {
        Tracks = tracks;
        _durationSeconds = Math.Max(0.0001, durationSeconds);   // a zero duration would divide by zero on the first tick
        _delaySeconds = Math.Max(0.0, delaySeconds);
        _iterationCount = iterationCount;
        _autoReverse = autoReverse;
        _easing = easing;
    }

    public Track[] Tracks { get; }

    /// <summary>True once <paramref name="elapsedSeconds"/> is past the last iteration. An infinite animation never is.</summary>
    public bool IsFinished(double elapsedSeconds)
    {
        if (double.IsPositiveInfinity(_iterationCount)) return false;
        var active = elapsedSeconds - _delaySeconds;
        return active > 0.0 && active / _durationSeconds >= _iterationCount;
    }

    /// <summary>The value of <paramref name="track"/> at <paramref name="elapsedSeconds"/> since the animation began.
    /// Clamped at both ends: before the delay it holds the start value, past the end it holds the final one.</summary>
    public double Evaluate(Track track, double elapsedSeconds) =>
        Interpolate(track, CyclePosition(Progress(elapsedSeconds)));

    // Iterations completed so far (fractional), clamped to the last one. Still inside the delay -> 0, i.e. hold the start.
    private double Progress(double elapsedSeconds)
    {
        var active = elapsedSeconds - _delaySeconds;
        if (active <= 0.0) return 0.0;

        var cycles = active / _durationSeconds;
        return !double.IsPositiveInfinity(_iterationCount) && cycles >= _iterationCount ? _iterationCount : cycles;
    }

    /// <summary>The eased position within the current iteration, 0..1. Integer boundaries are the END of the previous
    /// iteration; with AutoReverse the odd iterations run backwards.</summary>
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
}
