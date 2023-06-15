using System.Collections.Generic;
using Adamantium.Mathematics.Triangulation;

namespace Adamantium.Mathematics;

public static class ContourProcessingHelper
{
    public static void ProcessContoursIntersections(List<GeometrySegment> contoursSegments1, List<GeometrySegment> contoursSegments2)
    {
        var intersectionsList = new Dictionary<Vector2, GeometryIntersection>();
        var mesh1Intersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();
        var mesh2Intersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();
        
        foreach (var segment1 in contoursSegments1)
        {
            mesh1Intersections[segment1] = new SortedList<double, GeometryIntersection>();
            
            // add start and end points of segment1
            ProcessIntersection(segment1.Start, intersectionsList, mesh1Intersections, segment1);
            ProcessIntersection(segment1.End, intersectionsList, mesh1Intersections, segment1);
            
            foreach (var segment2 in contoursSegments2)
            {
                if (!mesh2Intersections.ContainsKey(segment2))
                {
                    mesh2Intersections[segment2] = new SortedList<double, GeometryIntersection>();
                }
                
                // add start and end points of segment2
                ProcessIntersection(segment2.Start, intersectionsList, mesh2Intersections, segment2);
                ProcessIntersection(segment2.End, intersectionsList, mesh2Intersections, segment2);
                
                // add intersection points/
                if (Collision2D.SegmentSegmentIntersection(segment1, segment2, out var point))
                {
                    ProcessIntersection(point, intersectionsList, mesh1Intersections, segment1);
                    ProcessIntersection(point, intersectionsList, mesh2Intersections, segment2);
                }
            }
        }

        FillMeshSegments(mesh1Intersections);
        FillMeshSegments(mesh2Intersections);
    }
    
    private static void ProcessIntersection(Vector2 point, 
                                            Dictionary<Vector2, GeometryIntersection> intersectionsList,
                                            Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>> meshIntersections,
                                            GeometrySegment segment)
    {
        if (!intersectionsList.ContainsKey(point))
        {
            intersectionsList[point] = new GeometryIntersection(point);
        }

        var distanceToStart = (point - segment.Start).Length();
        if (!meshIntersections[segment].ContainsKey(distanceToStart)) meshIntersections[segment].Add(distanceToStart, intersectionsList[point]);
    }
    
    private static void FillMeshSegments(Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>> meshIntersections)
    {
        foreach (var pair in meshIntersections)
        {
            if (pair.Value.Count < 2) continue;

            pair.Key.RemoveSelfFromConnectedSegments();
            pair.Key.RemoveSelfFromParent();

            for (var i = 0; i < pair.Value.Values.Count - 1; i++)
            {
                var newStart = pair.Value.Values[i];
                var newEnd = pair.Value.Values[i + 1];
                var newSegment = new GeometrySegment(pair.Key.Parent, newStart, newEnd);
                pair.Key.Parent.Segments.Add(newSegment);
            }
        }
    }
    
    // Check position of second contour's segments against first contour's segments (if contour 2 partially or fully inside or outside of contour 1) 
    public static List<GeometrySegment> MarkSegments(List<GeometrySegment> contourSegments, List<GeometrySegment> checkedContourSegments)
    {
        var arguableSegments = new List<GeometrySegment>();
        
        foreach (var segment2 in checkedContourSegments)
        {
            var segmentCenter = (segment2.End - segment2.Start) * 0.5 + segment2.Start;
            
            var ray = new Ray2D(segmentCenter, Vector2.UnitX);
            var intersectionCnt = 0;
            var rayIntersectionPoints = new HashSet<Vector2>();
            
            foreach (var segment1 in contourSegments)
            {
                if (segment1.IsEqualTo(segment2))
                {
                    segment2.IsArguable = true;
                    arguableSegments.Add(segment2);
                    
                    segment1.RemoveSelfFromConnectedSegments();
                    segment1.RemoveSelfFromParent();
                    
                    break;
                }

                var seg = new LineSegment2D(segment1);
                if (Collision2D.RaySegmentIntersection(ref ray, ref seg, out var intPoint))
                {
                    intPoint = Vector2.Round(intPoint, 4);
                    
                    if (!rayIntersectionPoints.Contains(intPoint))
                    {
                        rayIntersectionPoints.Add(intPoint);
                        ++intersectionCnt;
                    }
                }
            }

            if (segment2.IsArguable) continue;
            
            if (intersectionCnt % 2 != 0)
            {
                segment2.IsInner = true;
            }
        }

        return arguableSegments;
    }

    public static void ResolveArguableSegments(List<GeometrySegment> arguableSegments, List<GeometrySegment> contoursSegments)
    {
        if (arguableSegments == null || arguableSegments.Count < 1) return;
        if (contoursSegments == null || contoursSegments.Count < 3) return; // because area is formed with at least 3 segments

        foreach (var arguableSeg in arguableSegments)
        {
            arguableSeg.IsInner = Collision2D.IsSegmentInsideArea(arguableSeg, contoursSegments);
        }
    }
}