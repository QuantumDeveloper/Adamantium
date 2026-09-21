using Adamantium.Core.Collections;

namespace Adamantium.UI.Core.Resources;

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

    public override string ToString()
    {
        var result = string.Join(' ', this);
        
        return result;
    }
}
