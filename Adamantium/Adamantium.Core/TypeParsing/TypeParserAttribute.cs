using System;

namespace Adamantium.Core.TypeParsing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class TypeParserAttribute : Attribute
{
    public Type ParserType { get; }

    public TypeParserAttribute(Type parserType)
    {
        ParserType = parserType;
    }
}