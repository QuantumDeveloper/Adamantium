using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core;

public static class TypeCastFactory
{
    public static object CastFromString(object input, Type finalType)
    {
        if (input == null || input.GetType() == finalType) return input;

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
}