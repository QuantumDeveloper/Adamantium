using System.Globalization;
using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>Parses a <see cref="Cue"/> from a fraction ("0.5") or a percentage ("50%"). Invariant culture.</summary>
public class CueParser : ITypeParser<Cue>
{
    public Cue Parse(string value)
    {
        value = value.Trim();
        return value.EndsWith("%")
            ? new Cue(double.Parse(value[..^1], CultureInfo.InvariantCulture) / 100.0)
            : new Cue(double.Parse(value, CultureInfo.InvariantCulture));
    }
}
