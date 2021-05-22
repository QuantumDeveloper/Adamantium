using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    public class SampledOutline
    {
        public Vector2D[] Points { get; }

        public SampledOutline(Vector2D[] points)
        {
            Points = points;
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