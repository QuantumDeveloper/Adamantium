using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Core.TypeParsing;

public static class ParserRegistry
{
    private static readonly Dictionary<Type, Type> _parsers;

    static ParserRegistry()
    {
        _parsers = new Dictionary<Type, Type>();
        Register(typeof(Color), typeof(ColorParser));
        Register(typeof(Thickness), typeof(ThicknessParser));
        Register(typeof(Vector2), typeof(Vector2Parser));
        Register(typeof(Uri), typeof(UriParser));
    }

    public static void Register(Type targetType, Type parserType)
    {
        _parsers[targetType] = parserType;
    }

    public static Type GetParserFor(Type targetType)
    {
        _parsers.TryGetValue(targetType, out var parserType);
        return parserType;
    }
}