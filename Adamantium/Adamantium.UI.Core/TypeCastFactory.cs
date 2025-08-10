using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core;

public static class TypeCastFactory
{
    // TODO: change this to TypeParser class
    public static object CastFromString(object input, Type finalType)
    {
        if (input.GetType() == finalType) return input;
        
        if (finalType.IsPrimitive)
        {
            if (finalType == typeof(Double) && input.ToString() == "Auto")
            {
                return Double.NaN;
            }
            return Convert.ChangeType(input, finalType);
        }
        if (finalType.IsSubclassOf(typeof(Brush)) || finalType == typeof(Brush))
        {
            return TypeParser.Parse<Brush>(input.ToString());
        }
        if (finalType == typeof(Thickness))
        {
            return TypeParser.Parse<Thickness>(input.ToString());
        }

        throw new NotSupportedException($"Casting {input} to {finalType.Name} is not supported");
    }
}