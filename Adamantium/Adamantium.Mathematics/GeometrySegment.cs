using System;
using System.Collections.Generic;

namespace Adamantium.Mathematics;

public class GeometrySegment
{
    public MeshContour Parent { get; set; }
    public List<GeometryIntersection> SegmentEnds { get; set; }
    public bool IsInnerSegment { get; set; }

    public Vector2 Start => SegmentEnds[0].Coordinates;
    public Vector2 End => SegmentEnds[1].Coordinates;
    
    public Vector2 Direction { get; }
    public Vector2 DirectionNormalized { get; }

    public GeometrySegment(MeshContour parent, GeometryIntersection end1, GeometryIntersection end2)
    {
        Parent = parent;
        
        Direction = end2.Coordinates - end1.Coordinates;
        DirectionNormalized = Vector2.Normalize(Direction);
        
        SegmentEnds = new List<GeometryIntersection>
        {
            end1,
            end2
        };

        AddSelfToConnectedSegments();
    }
    
    public GeometrySegment(MeshContour parent, Vector2 point1, Vector2 point2)
    {
        Parent = parent;
        
        Direction = point2 - point1;
        DirectionNormalized = Vector2.Normalize(Direction);
        
        var intersection1 = new GeometryIntersection(point1);
        var intersection2 = new GeometryIntersection(point2);

        SegmentEnds = new List<GeometryIntersection>
        {
            intersection1,
            intersection2
        };

        AddSelfToConnectedSegments();
    }

    public void AddSelfToConnectedSegments()
    {
        if (!SegmentEnds[0].ConnectedSegments.Contains(this)) SegmentEnds[0].ConnectedSegments.Add(this);
        if (!SegmentEnds[1].ConnectedSegments.Contains(this)) SegmentEnds[1].ConnectedSegments.Add(this);
    }
    
    public void RemoveSelfFromConnectedSegments()
    {
        if (SegmentEnds[0].ConnectedSegments.Contains(this)) SegmentEnds[0].ConnectedSegments.Remove(this);
        if (SegmentEnds[1].ConnectedSegments.Contains(this)) SegmentEnds[1].ConnectedSegments.Remove(this);
    }

    public void RemoveSelfFromParent()
    {
        if (Parent != null && Parent.Segments.Contains(this)) Parent.Segments.Remove(this);
    }
    
    public GeometrySegment GetNextSegment()
    {
        foreach (var segment in SegmentEnds[1].ConnectedSegments)
        {
            if (segment != this && segment.Parent == Parent) return segment;
        }

        return null;
    }
    
    public GeometrySegment GetPrevSegment()
    {
        foreach (var segment in SegmentEnds[0].ConnectedSegments)
        {
            if (segment != this && segment.Parent == Parent) return segment;
        }

        return null;
    }

    public GeometryIntersection GetOtherEnd(GeometryIntersection end)
    {
        if (SegmentEnds.Count < 2) return null;
        return SegmentEnds[0] == end ? SegmentEnds[1] : SegmentEnds[0];
    }
}