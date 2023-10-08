using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Resources;

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
}
