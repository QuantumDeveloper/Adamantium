using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    internal class OutlineSegment
    {
        public List<Vector2> Points { get; }

        public OutlineSegment()
        {
            Points = new List<Vector2>();
        }
        
        public OutlineSegment(List<Vector2> points)
        {
            Points = points;
        }

        public void AddPoint(Vector2 point)
        {
            Points.Add(point);
        }
    }
}