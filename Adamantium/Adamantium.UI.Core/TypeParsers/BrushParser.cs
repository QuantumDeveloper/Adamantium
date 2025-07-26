using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.TypeParsers;

public class BrushParser : ITypeParser<SolidColorBrush>
{
    public SolidColorBrush Parse(string value)
    {
        return new SolidColorBrush(value);
    }
}