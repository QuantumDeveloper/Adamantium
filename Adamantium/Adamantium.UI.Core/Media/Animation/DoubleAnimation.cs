using System;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// Describes an animation of a <see cref="double"/> AdamantiumProperty from <see cref="From"/> to <see cref="To"/> over
/// <see cref="Duration"/>, shaped by <see cref="Easing"/>. Start it with <see cref="AdamantiumComponent.BeginAnimation"/>.
/// </summary>
public sealed class DoubleAnimation
{
    public double From { get; set; }

    public double To { get; set; }

    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Wait this long before the first iteration starts (the From value is held during the delay).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>How many times to play; <see cref="double.PositiveInfinity"/> loops forever. Default 1.</summary>
    public double IterationCount { get; set; } = 1;

    /// <summary>When true, every other iteration plays backwards (To-&gt;From), i.e. ping-pong.</summary>
    public bool AutoReverse { get; set; }

    /// <summary>Easing curve; null means linear.</summary>
    public IEasingFunction Easing { get; set; }
}
