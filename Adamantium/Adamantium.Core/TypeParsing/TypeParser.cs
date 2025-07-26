using System;
using System.Reflection;

namespace Adamantium.Core.TypeParsing;

public static class TypeParser
{
    public static TTarget Parse<TTarget>(string value)
    {
        var targetType = typeof(TTarget);
        
        Type parserType = ParserRegistry.GetParserFor(targetType);

        if (parserType == null)
        {
            var attribute = targetType.GetCustomAttribute<TypeParserAttribute>();
            if (attribute != null)
            {
                parserType = attribute.ParserType;
            }
        }

        if (parserType == null)
        {
            throw new InvalidOperationException($"Type parser not found for {targetType.FullName}");
        }

        try
        {
            var parser = (ITypeParser<TTarget>)Activator.CreateInstance(parserType);
            return parser.Parse(value);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Cannot parse {value} as input for {targetType.FullName}", e);
        }
    }
}