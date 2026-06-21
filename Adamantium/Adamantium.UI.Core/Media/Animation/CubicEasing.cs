using System;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>Cubic easing (t^3). The default for slide transitions: a smooth accelerate/decelerate.</summary>
public sealed class CubicEasing : IEasingFunction
{
    public EasingMode Mode { get; set; } = EasingMode.InOut;

    public double Ease(double progress)
    {
        var t = Math.Clamp(progress, 0.0, 1.0);
        return Mode switch
        {
            EasingMode.In => t * t * t,
            EasingMode.Out => 1.0 - Math.Pow(1.0 - t, 3),
            _ => t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3) / 2.0,
        };
    }
}
