using System;
using Adamantium.UI.Media;

namespace Adamantium.UI;

public static class TypeCastFactory
{
    public static object CastFromString(object input, Type finalType)
    {
        if (input.GetType() == finalType) return input;
        
        if (finalType.IsPrimitive)
        {
            return Convert.ChangeType(input, finalType);
        }
        if (finalType.IsSubclassOf(typeof(Brush)) || finalType == typeof(Brush))
        {
            return Brush.Parse(input.ToString());
        }
        if (finalType == typeof(Thickness))
        {
            return Thickness.Parse(input.ToString());
        }

        throw new NotSupportedException($"Casting {input} to {finalType.Name} is not supported");
    }
}