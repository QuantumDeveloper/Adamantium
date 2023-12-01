using MessagePack;
using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    internal class Outline
    {
        [Key(0)]
        public List<OutlinePoint> Points { get; }
        [Key(1)]
        public List<OutlineSegment> Segments { get; }

        [Key(2)]
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
