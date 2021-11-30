using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.PaletteQuantizer.PathProviders
{
    public class SerpentinePathProvider : IPathProvider
    {
        public IList<Vector2D> GetPointPath(Int32 width, Int32 height)
        {
            Boolean leftToRight = true;
            var result = new List<Vector2D>(width * height);

            for (Int32 y = 0; y < height; y++)
            {
                for (Int32 x = leftToRight ? 0 : width - 1; leftToRight ? x < width : x >= 0; x += leftToRight ? 1 : -1)
                {
                    var point = new Vector2D(x, y);
                    result.Add(point);
                }

                leftToRight = !leftToRight;
            }

            return result;
        }
    }
}
