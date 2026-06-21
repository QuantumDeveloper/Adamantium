using System;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>One in-flight <see cref="DoubleAnimation"/> bound to a target property. Advanced each frame by
/// <see cref="AnimationManager"/>; writes the interpolated value at <see cref="ValuePriority.Animation"/> (the highest
/// priority, so it overrides Local/Binding while it runs) and holds the final value on completion.</summary>
internal sealed class RunningAnimation
{
    private readonly AdamantiumComponent _target;
    private readonly AdamantiumProperty _property;
    private readonly double _from;
    private readonly double _to;
    private readonly double _durationSeconds;
    private readonly double _delaySeconds;
    private readonly double _iterationCount;   // double.PositiveInfinity = loop forever
    private readonly bool _autoReverse;
    private readonly IEasingFunction _easing;
    private readonly Action _completed;
    private double _elapsedSeconds;

    public RunningAnimation(AdamantiumComponent target, AdamantiumProperty property, DoubleAnimation animation, Action completed)
    {
        _target = target;
        _property = property;
        _from = animation.From;
        _to = animation.To;
        _durationSeconds = Math.Max(0.0001, animation.Duration.TotalSeconds);
        _delaySeconds = Math.Max(0.0, animation.Delay.TotalSeconds);
        _iterationCount = animation.IterationCount;
        _autoReverse = animation.AutoReverse;
        _easing = animation.Easing;
        _completed = completed;
    }

    public bool Is(AdamantiumComponent target, AdamantiumProperty property) =>
        ReferenceEquals(_target, target) && _property == property;

    /// <summary>Advances by <paramref name="deltaSeconds"/>; returns true once finished (final value applied,
    /// completion callback fired). Honours Delay, IterationCount (incl. infinite) and AutoReverse (ping-pong).</summary>
    public bool Advance(double deltaSeconds)
    {
        _elapsedSeconds += deltaSeconds;

        var active = _elapsedSeconds - _delaySeconds;
        if (active <= 0.0)
        {
            // Still in the delay: hold the start value so the element is positioned before the animation begins.
            _target.SetValue(_property, EvalAt(0.0), ValuePriority.Animation);
            return false;
        }

        var cycles = active / _durationSeconds;
        if (!double.IsPositiveInfinity(_iterationCount) && cycles >= _iterationCount)
        {
            _target.SetValue(_property, EvalAt(_iterationCount), ValuePriority.Animation);   // hold the final value
            _completed?.Invoke();
            return true;
        }

        _target.SetValue(_property, EvalAt(cycles), ValuePriority.Animation);
        return false;
    }

    /// <summary>The interpolated value at <paramref name="progress"/> iterations (0 = start). Integer boundaries are
    /// the END of the previous iteration; with <see cref="_autoReverse"/> odd iterations run backwards.</summary>
    private double EvalAt(double progress)
    {
        var iteration = Math.Floor(progress);
        var localT = progress - iteration;
        if (localT == 0.0 && progress > 0.0)
        {
            iteration -= 1;
            localT = 1.0;
        }

        var reversed = _autoReverse && (long)iteration % 2 == 1;
        var directionalT = reversed ? 1.0 - localT : localT;
        var eased = _easing?.Ease(directionalT) ?? directionalT;
        return _from + (_to - _from) * eased;
    }
}
