using System;

namespace Adamantium.Core.TypeParsing;

public class UriParser : ITypeParser<Uri>
{
    public Uri Parse(string value)
    {
        return new Uri(value, UriKind.RelativeOrAbsolute);
    }
}