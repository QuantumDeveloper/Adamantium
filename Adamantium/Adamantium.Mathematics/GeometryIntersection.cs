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
        return HashCode.Combine(Coordinates.X, Coordinates.Y);
    }

    public override string ToString()
    {
        return $"Coordinates: {Coordinates} ConnectedSegments.Count: {ConnectedSegments.Count}";
    }

    public GeometrySegment GetOtherSegment(GeometrySegment segment)
    {
        if (ConnectedSegments.Count < 2) return null;
        return ConnectedSegments[0] == segment ? ConnectedSegments[1] : ConnectedSegments[0];
    }
}