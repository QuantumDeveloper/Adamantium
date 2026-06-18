using System;
using System.Reflection;

namespace Adamantium.Core.TypeParsing;

public static class TypeParser
{
    public static TTarget Parse<TTarget>(string value) => (TTarget)Parse(value, typeof(TTarget));

    /// <summary>
    /// Non-generic counterpart of <see cref="Parse{TTarget}"/>: parses <paramref name="value"/> into
    /// <paramref name="targetType"/> using its registered parser (ParserRegistry) or its <c>[TypeParser]</c>
    /// attribute. Lets callers that only have a runtime <see cref="Type"/> (e.g. value-to-property coercion) use
    /// the same conversion a compiled build does.
    /// </summary>
    public static object Parse(string value, Type targetType)
    {
        var parserType = ParserRegistry.GetParserFor(targetType)
                         ?? targetType.GetCustomAttribute<TypeParserAttribute>()?.ParserType;

        if (parserType == null)
        {
            throw new InvalidOperationException($"Type parser not found for {targetType.FullName}");
        }

        try
        {
            var parser = Activator.CreateInstance(parserType);
            var parse = parserType.GetMethod(nameof(ITypeParser<object>.Parse), [typeof(string)]);
            return parse!.Invoke(parser, [value]);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Cannot parse {value} as input for {targetType.FullName}",
                e is TargetInvocationException tie ? tie.InnerException : e);
        }
    }
}