using System.Collections.Generic;
using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    internal class OutlineSegment
    {
        [Key(0)]
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