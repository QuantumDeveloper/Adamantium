using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core;

public static class TypeCastFactory
{
    public static object CastFromString(object input, Type finalType)
    {
        if (input == null || input.GetType() == finalType) return input;

        // Nullable<T> (e.g. ToggleButton.IsChecked is bool?): an empty/"null" string is null, otherwise parse the
        // underlying T - a boxed T is a valid boxed Nullable<T>. Done before the TypeParser fall-through, which has no
        // parser for Nullable<…>.
        var underlyingType = Nullable.GetUnderlyingType(finalType);
        if (underlyingType != null)
        {
            var text = input as string ?? input.ToString();
            if (string.IsNullOrEmpty(text) || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
                return null;
            return CastFromString(input, underlyingType);
        }

        // A malformed value in markup / a style setter (e.g. Height="NaN", a typo'd number/enum) must NOT crash the whole
        // app: catch the conversion failure, log it, and return UnsetValue so the caller skips it (SetValue treats
        // UnsetValue as "clear", and value resolution skips it - so the property keeps its default / lower-priority value).
        try
        {
            if (finalType.IsPrimitive)
            {
                if (finalType == typeof(Double) && input.ToString() == "Auto")
                {
                    return Double.NaN;
                }
                return Convert.ChangeType(input, finalType);
            }

            if (finalType.IsEnum) return Enum.Parse(finalType, input.ToString(), ignoreCase: true);

            // Everything else (Brush, Thickness, CornerRadius, Color, Vector2, Geometry, …) converts through the engine's
            // TypeParser - honouring [TypeParser] + the ParserRegistry - i.e. the same conversion a compiled build uses.
            return TypeParser.Parse(input.ToString(), finalType);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[TypeCastFactory] could not convert '{input}' to {finalType.Name} - value ignored: {ex.Message}");
            return AdamantiumProperty.UnsetValue;
        }
    }
}