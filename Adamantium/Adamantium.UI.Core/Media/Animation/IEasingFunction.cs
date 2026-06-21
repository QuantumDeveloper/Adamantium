using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>Maps a linear progress in [0,1] to an eased progress in [0,1]. In markup written as a name
/// (e.g. <c>Easing="CubicOut"</c>), parsed by <see cref="EasingParser"/>.</summary>
[TypeParser(typeof(EasingParser))]
public interface IEasingFunction
{
    double Ease(double progress);
}
