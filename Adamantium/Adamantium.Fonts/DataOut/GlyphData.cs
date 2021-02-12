using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.DataOut
{
    public class GlyphData
    {
        public bool IsInvalid { get; set; } // set this flag if glyph was not properly loaded

        public ushort AdvanceWidth { get; internal set; }
        public short LeftSideBearing { get; internal set; }

        public List<GlyphContour> BezierContours { get; internal set; } // final generated bezier contours
        public List<Vector3F> Vertices { get; internal set; } // triangulated vertices

        internal GlyphData()
        {
            BezierContours = new List<GlyphContour>();
            Vertices = new List<Vector3F>();
        }
    }
}
