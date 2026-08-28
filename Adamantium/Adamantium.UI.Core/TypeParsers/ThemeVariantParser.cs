using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.TypeParsers;

/// <summary>Reads <c>ThemeContext.Variant="Dark"</c> from markup. The variant's key is taken at its word - a theme may
/// name its variants whatever it likes, and whether the key EXISTS is a question only the theme in force can answer.</summary>
public class ThemeVariantParser : ITypeParser<ThemeVariant>
{
    public ThemeVariant Parse(string value) => ThemeVariant.Parse(value);
}
