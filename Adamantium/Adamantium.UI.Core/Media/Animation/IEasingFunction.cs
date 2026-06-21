namespace Adamantium.UI.Core.Media.Animation;

/// <summary>Maps a linear progress in [0,1] to an eased progress in [0,1].</summary>
public interface IEasingFunction
{
    double Ease(double progress);
}
