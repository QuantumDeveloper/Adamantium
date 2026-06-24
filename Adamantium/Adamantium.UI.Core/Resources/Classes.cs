using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Resources;

[TypeParser(typeof(ClassesParser))]
public class  Classes : TrackingCollection<string>
{
    public Classes()
    {

    }

    public Classes(IEnumerable<string> collection): base(collection)
    {

    }

    public static Classes Parse(string identifierString)
    {
        var ids = identifierString.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

        if (ids.Length == 0) return null;

        return new Classes(ids);
    }
    
    public override string ToString()
    {
        return string.Join(' ', this);
    }
}
