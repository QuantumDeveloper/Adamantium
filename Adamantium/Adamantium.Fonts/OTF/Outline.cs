using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public class Outline
    {
        public List<OutlinePoint> Points;
        public List<OutlineSegment> Segments;

        public Outline()
        {
            Points = new List<OutlinePoint>();
            Segments = new List<OutlineSegment>();
        }
    }
}
