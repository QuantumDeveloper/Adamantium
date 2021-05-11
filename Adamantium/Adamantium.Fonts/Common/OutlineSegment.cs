using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    internal class OutlineSegment
    {
        public List<Vector2D> Points { get; }

        public OutlineSegment()
        {
            Points = new List<Vector2D>();
        }
        
        public OutlineSegment(List<Vector2D> points)
        {
            Points = points;
        }

        public void AddPoint(Vector2D point)
        {
            Points.Add(point);
        }
    }
}