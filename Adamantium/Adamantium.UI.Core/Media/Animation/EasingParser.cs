using System;
using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// Parses an <see cref="IEasingFunction"/> from a friendly name used in markup (e.g. <c>Easing="CubicOut"</c>).
/// Supported: <c>Linear</c>, <c>CubicIn</c>/<c>CubicOut</c>/<c>CubicInOut</c> (aliases <c>EaseIn</c>/<c>EaseOut</c>/
/// <c>EaseInOut</c>; bare <c>Cubic</c> = InOut).
/// </summary>
public class EasingParser : ITypeParser<IEasingFunction>
{
    public IEasingFunction Parse(string value)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "linear": return new LinearEasing();
            case "cubicin": case "easein": return new CubicEasing { Mode = EasingMode.In };
            case "cubicout": case "easeout": return new CubicEasing { Mode = EasingMode.Out };
            case "cubic": case "cubicinout": case "easeinout": return new CubicEasing { Mode = EasingMode.InOut };
            default: throw new FormatException($"Unknown easing '{value}'. Use Linear, CubicIn, CubicOut or CubicInOut.");
        }
    }
}
