using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.TypeParsers;

public class ClassesParser : ITypeParser<Classes>
{
    public Classes Parse(string value) => Classes.Parse(value);
}
