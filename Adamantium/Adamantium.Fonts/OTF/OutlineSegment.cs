using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public class OutlineSegment
    {
        public List<Vector2D> Points;

        public OutlineSegment()
        {
            Points = new List<Vector2D>();
        }
        
        public OutlineSegment(List<Vector2D> points)
        {
            Points = points;
        }
    }
}