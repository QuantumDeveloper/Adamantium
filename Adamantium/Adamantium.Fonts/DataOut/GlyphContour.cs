using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.DataOut
{
    public class GlyphContour
    {
        public List<Vector2D> Points { get; internal set; }

        internal GlyphContour()
        {
            Points = new List<Vector2D>();
        }
    }
}
