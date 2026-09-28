using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class Contour
    {
        public List<ContourSegment> Segments { get; set; } // could be a line or a curve
        public UInt16 NumberOfPoints { get; set; } // number of points for current contour within glyph

        public Contour()
        {
            Segments = new List<ContourSegment>();
        }
    };
}
