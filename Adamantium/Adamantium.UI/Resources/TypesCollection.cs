using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Resources;

public class TypesCollection : TrackingCollection<Type>
{
    public TypesCollection()
    {

    }

    public TypesCollection(IEnumerable<Type> collection) : base(collection)
    {

    }

    public static Classes Parse(string identifierString)
    {
        var ids = identifierString.Split(' ', StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

        if (ids.Length == 0) return null;

        return new Classes(ids);
    }
}
