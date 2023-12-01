using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    public class SampledOutline
    {
        [Key(0)]
        public Vector2[] Points { get; }

        [Key(1)]
        public LineSegment2D[] Segments { get; }

        public SampledOutline(Vector2[] points)
        {
            Points = points;
        }

        public SampledOutline(LineSegment2D[] segments)
        {
            Segments = segments;
        }
        
        private SampledOutline()
        {
            
        }

        static SampledOutline()
        {
            Empty = new SampledOutline();
        }

        public static SampledOutline Empty;
    }
}