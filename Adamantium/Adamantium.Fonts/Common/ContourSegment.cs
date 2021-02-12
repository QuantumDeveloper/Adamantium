using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class ContourSegment
    {
        // each point is a delta to previous point or to (0, 0) if it's the first point
        public List<Point> Points { get; set; } // size() == 2 points - line, size() == 3 points - bezier quadratic curve

        public ContourSegment()
        {
            Points = new List<Point>();
        }
    };
}
