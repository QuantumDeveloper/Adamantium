using System.Collections.Generic;

namespace Adamantium.Mathematics;

public class SegmentGroup
{
    public SegmentGroup()
    {
        Segments = new List<LineSegment2D>();
    }
            
    public List<LineSegment2D> Segments { get; set; }
}