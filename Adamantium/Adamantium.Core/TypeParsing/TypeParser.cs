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
        // A Nullable<T> property (e.g. ToggleButton.IsChecked = bool?) has no parser of its own: an empty / "null"
        // value is the null state, otherwise parse the underlying T - the boxed result unboxes straight into the
        // nullable (so IsChecked="True" in markup works instead of throwing "parser not found for Nullable<Boolean>").
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                return null;
            targetType = underlying;
        }

        var parserType = ParserRegistry.GetParserFor(targetType)
                         ?? targetType.GetCustomAttribute<TypeParserAttribute>()?.ParserType;

        if (parserType == null)
        {
            // Primitives (bool/int/double/enum...) are emitted inline by codegen and normally never reach here - EXCEPT
            // as the underlying T of a Nullable<T> (e.g. IsChecked="True" -> bool?, which has no parser of its own).
            // Fall back to the framework converter for those instead of failing.
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, ignoreCase: true);
            if (typeof(IConvertible).IsAssignableFrom(targetType))
                return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
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