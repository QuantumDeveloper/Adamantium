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
        _easing = animation.Easing;
        _completed = completed;
    }

    public bool Is(AdamantiumComponent target, AdamantiumProperty property) =>
        ReferenceEquals(_target, target) && _property == property;

    /// <summary>Advances by <paramref name="deltaSeconds"/>; returns true once finished (final value applied,
    /// completion callback fired).</summary>
    public bool Advance(double deltaSeconds)
    {
        _elapsedSeconds += deltaSeconds;
        var t = _elapsedSeconds / _durationSeconds;
        if (t >= 1.0)
        {
            _target.SetValue(_property, _to, ValuePriority.Animation);
            _completed?.Invoke();
            return true;
        }

        var eased = _easing?.Ease(t) ?? t;
        _target.SetValue(_property, _from + (_to - _from) * eased, ValuePriority.Animation);
        return false;
    }
}
