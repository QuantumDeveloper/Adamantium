using System;
using System.Collections.Generic;

namespace Adamantium.Mathematics;

public class GeometryIntersection
{
    public List<GeometrySegment> ConnectedSegments { get; set; }
    public Vector2 Coordinates { get; set; }

    public GeometryIntersection(Vector2 coordinates)
    {
        ConnectedSegments = new List<GeometrySegment>();
        Coordinates = coordinates;
    }

    public override int GetHashCode()
    {
        return Coordinates.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is GeometryIntersection point)
        {
            return Coordinates == point.Coordinates;
        }

        return false;
    }

    public override string ToString()
    {
        return $"Coordinates: {Coordinates} ConnectedSegments.Count: {ConnectedSegments.Count}";
    }

    public GeometrySegment GetSegmentFromOtherParent(GeometrySegment segment)
    {
        if (ConnectedSegments.Count < 2) return null;

        foreach (var connectedSegment in ConnectedSegments)
        {
            if (segment != connectedSegment &&
                connectedSegment.IsAlreadyInTriangulatorContour == false &&
                segment.Parent != connectedSegment.Parent) return connectedSegment;
        }

        return null;
    }
    
    public GeometrySegment GetSegmentFromSameParent(GeometrySegment segment)
    {
        if (ConnectedSegments.Count < 2) return null;

        foreach (var connectedSegment in ConnectedSegments)
        {
            if (segment != connectedSegment &&
                connectedSegment.IsAlreadyInTriangulatorContour == false &&
                segment.Parent == connectedSegment.Parent) return connectedSegment;
        }

        return null;
    }

    public GeometrySegment GetAnyOtherSegment(GeometrySegment segment)
    {
        if (ConnectedSegments.Count < 2) return null;

        foreach (var connectedSegment in ConnectedSegments)
        {
            if (segment != connectedSegment && connectedSegment.IsAlreadyInTriangulatorContour == false) return connectedSegment;
        }

        return null;
    }
}