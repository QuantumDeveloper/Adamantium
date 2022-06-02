using Adamantium.Core.Collections;
using System.Collections.Generic;

namespace Adamantium.UI;

public class Classes : TrackingCollection<string>
{
    public Classes()
    {

    }

    public Classes(IEnumerable<string> collection): base(collection)
    {

    }

    public static Classes Parse(string classNames)
    {
        var classes = classNames.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

        if (classes.Length == 0) return null;

        return new Classes(classes);
    }
}
