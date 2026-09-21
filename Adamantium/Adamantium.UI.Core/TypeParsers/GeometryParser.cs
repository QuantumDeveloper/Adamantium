using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.TypeParsers;

public class GeometryParser : ITypeParser<Geometry>
{
    public Geometry Parse(string xamlString)
    {
        return new SVGParser().Parse(xamlString); 
    }
}