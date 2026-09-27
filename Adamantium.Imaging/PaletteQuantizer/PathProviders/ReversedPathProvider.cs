using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.PaletteQuantizer.PathProviders
{
    public class ReversedPathProvider : IPathProvider
    {
        public IList<Vector2> GetPointPath(Int32 width, Int32 height)
        {
            var result = new List<Vector2>(width*height);

            for (Int32 y = height - 1; y >= 0; y--)
            for (Int32 x = width - 1; x >= 0; x--)
            {
                var point = new Vector2(x, y);
                result.Add(point);
            }

            return result;
        }
    }
}
