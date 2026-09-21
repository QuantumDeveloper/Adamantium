using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Mathematics;

public static class VectorExtensions
{
    public static Vector3[] Round(this IEnumerable<Vector3> vectors, uint precision = 3)
    {
        var array = vectors.ToArray();
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = Vector3.Round(array[i], (int)precision);
        }
        
        return array.ToArray();
    }
}