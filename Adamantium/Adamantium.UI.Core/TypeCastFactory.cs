using Adamantium.Core.TypeParsing;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core;

public static class TypeCastFactory
{
    // TODO: change this to TypeParser class
    public static object CastFromString(object input, Type finalType)
    {
        if (input.GetType() == finalType) return input;
        
        if (finalType.IsPrimitive)
        {
            return Convert.ChangeType(input, finalType);
        }
        if (finalType.IsSubclassOf(typeof(Brush)) || finalType == typeof(Brush))
        {
            return new BrushParser().Parse(input.ToString());
        }
        if (finalType == typeof(Thickness))
        {
            return new ThicknessParser().Parse(input.ToString());
        }

        throw new NotSupportedException($"Casting {input} to {finalType.Name} is not supported");
    }
}