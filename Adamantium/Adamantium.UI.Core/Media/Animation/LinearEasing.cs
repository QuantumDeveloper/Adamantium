namespace Adamantium.UI.Core.Media.Animation;

/// <summary>No easing - progress passes through unchanged (constant speed).</summary>
public sealed class LinearEasing : IEasingFunction
{
    public double Ease(double progress) => progress;
}
