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

    /// <summary>Easing curve; null means linear.</summary>
    public IEasingFunction Easing { get; set; }
}
