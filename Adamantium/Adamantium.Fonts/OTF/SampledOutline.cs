using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public class SampledOutline
    {
        public Vector2D[] Points { get; }

        public SampledOutline(Vector2D[] points)
        {
            Points = points;
        }
    }
}