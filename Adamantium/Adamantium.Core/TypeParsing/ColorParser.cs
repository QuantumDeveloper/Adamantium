using Adamantium.Mathematics;

namespace Adamantium.Core.TypeParsing;

public class ColorParser : ITypeParser<Color>
{
    public Color Parse(string value)
    {
        return Colors.Get(value);
    }
}