using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core.TypeParsers;

public class RectParser : ITypeParser<Rect>
{
    public Rect Parse(string value)
    {
        return Rect.Parse(value);
    }
}
