using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public class Outline
    {
        public List<OutlinePoint> Points;

        public Outline()
        {
            Points = new List<OutlinePoint>();
        }
    }
}
