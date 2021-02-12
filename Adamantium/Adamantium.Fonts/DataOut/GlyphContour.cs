using System.Collections.Generic;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.DataOut
{
    public class GlyphContour
    {
        public List<Point> Points { get; internal set; }

        internal GlyphContour()
        {
            Points = new List<Point>();
        }
    }
}
