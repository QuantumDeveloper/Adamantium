using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class Outline
    {
        public List<OutlinePoint> Points { get; }
        public List<OutlineSegment> Segments { get; }
        
        // Used for TTF
        public UInt16 NumberOfPoints { get; set; }

        public Outline()
        {
            Points = new List<OutlinePoint>();
            Segments = new List<OutlineSegment>();
        }

        public void AddSegment(OutlineSegment segment)
        {
            Segments.Add(segment);
        }
    }
}
