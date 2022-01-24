using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Mathematics;

public class MeshContour
{
    public string Name { get; set; }
    public Vector2[] Points { get; set; }

    public List<GeometryIntersection> GeometryPoints { get; set; }

    public List<GeometrySegment> Segments { get; set; }

    public bool IsGeometryClosed { get; set; }

    public RectangleF BoundingBox { get; private set; }

    public MeshContour()
    {

    }

    public MeshContour(IEnumerable<Vector3> points, bool isGeometryClosed = true, bool generateSegments = false) :
        this()
    {
        var pts = new List<Vector2>();
        foreach (var vector3 in points)
        {
            pts.Add((Vector2)vector3);
        }

        IsGeometryClosed = isGeometryClosed;
        SetPoints(pts, generateSegments);
    }
    
    public MeshContour(IEnumerable<Vector2> points, bool isGeometryClosed = true, bool generateSegments = false) : this()
    {
        IsGeometryClosed = isGeometryClosed;
        SetPoints(points, generateSegments);
    }
    
    public MeshContour(IEnumerable<GeometrySegment> segments, bool isGeometryClosed = true) : this()
    {
        Segments = segments.ToList();
        
        IsGeometryClosed = isGeometryClosed;
        
        UpdatePoints();

        CalculateBoundingBox();
    }
    
    private void CalculateBoundingBox()
    {
        if (Points.Length > 0)
        {
            BoundingBox = RectangleF.FromPoints(Points.ToArray());
        }
    }
    
    public void SetOrUpdatePoints(IEnumerable<Vector2> points, bool generateSegments = true)
    {
        SetPoints(points, generateSegments);
    }

    public void SetPoints(IEnumerable<Vector2> points, bool generateSegments = true)
    {
        var tmpList = new List<Vector2>();
        var pts = points.ToArray();
        // Check for duplicating points going together and remove them
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var p1 = pts[i];
            var p2 = pts[i + 1];
            if (p1 != p2)
            {
                tmpList.Add(p1);
            }
            
            if (i + 1 == pts.Length - 1 && tmpList.Count > 0 && p2 != tmpList[0])
            {
                tmpList.Add(p2);
            }
        }
        
        Points = tmpList.ToArray();
        Segments?.Clear();
        if (generateSegments)
        {
            SplitOnSegments();
        }

        CalculateBoundingBox();
    }

    public void SplitOnSegments()
    {
        Segments = PolygonHelper.SplitOnSegments(this, Points, IsGeometryClosed);
        UpdatePoints();
    }
    
    public void UpdatePoints()
    {
        if (Segments == null || Segments.Count == 0)
        {
            GeometryPoints?.Clear();
            Points = Array.Empty<Vector2>();
            return;
        }

        var pointsHashSet = new HashSet<GeometryIntersection>();
        GeometryPoints = new List<GeometryIntersection>();
        var updatedPoints = new List<Vector2>();

        foreach (var segment in Segments)
        {
            foreach (var end in segment.SegmentEnds)
            {
                if (!pointsHashSet.Contains(end))
                {
                    pointsHashSet.Add(end);
                    GeometryPoints.Add(end);
                    updatedPoints.Add(end.Coordinates);
                }
            }
        }
        
        Points = updatedPoints.ToArray();

        CalculateBoundingBox();
    }
    
    public void UpdateSegmentsOrder()
    {
        if (Segments == null || Segments.Count == 0) return;

        var orderedSegments = new List<GeometrySegment>();
        
        var startSegment = Segments[0];
        var currentSegment = startSegment;

        do
        {
            orderedSegments.Add(currentSegment);
            currentSegment = currentSegment.GetNextSegment();
        } while (currentSegment != startSegment && currentSegment != null);

        Segments = orderedSegments;
        
        UpdatePoints();
    }

    public void RemoveSegmentsByRule(bool removeInner)
    {
        var tmpSegments = new List<GeometrySegment>(Segments);
        
        foreach (var segment in tmpSegments)
        {
            var delete = !(removeInner ^ segment.IsInnerSegment);
            if (delete)
            {
                segment.RemoveSelfFromConnectedSegments();
                segment.RemoveSelfFromParent();
            }
        }
        
        UpdatePoints();
    }

    public void RemoveSelfIntersections(FillRule fillRule)
    {
        var intersectionsList = new Dictionary<Vector2, GeometryIntersection>();
        var selfIntersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();

        foreach (var currentSegment in Segments)
        {
            selfIntersections[currentSegment] = new SortedList<double, GeometryIntersection>();
            
            foreach (var intersectSegment in Segments)
            {
                if (intersectSegment == currentSegment) continue;
                
                if (Collision2D.SegmentSegmentIntersection(currentSegment, intersectSegment, out var point))
                {
                    if (point != currentSegment.Start && point != currentSegment.End)
                    {
                        
                        
                        if (!intersectionsList.ContainsKey(point))
                        {
                            intersectionsList[point] = new GeometryIntersection(point);
                        }

                        var distanceToStart = (point - currentSegment.Start).Length();
                        selfIntersections[currentSegment].Add(distanceToStart, intersectionsList[point]);
                    }
                }
            }
        }

        Segments.Clear();
        
        foreach (var (key, value) in selfIntersections)
        {
            if (value.Count == 0)
            {
                Segments.Add(key);
                continue;
            }

            key.RemoveSelfFromConnectedSegments();
            
            var startPart = new GeometrySegment(key.Parent, key.SegmentEnds[0], value.Values.First());
            Segments.Add(startPart);
            
            if (fillRule == FillRule.EvenOdd)
            {
                for (var i = 0; i < value.Values.Count - 1; i++)
                {
                    var int1 = value.Values[i];
                    var int2 = value.Values[i + 1];

                    var seg = new GeometrySegment(key.Parent, int1, int2);
                    key.Parent.Segments.Add(seg);
                }
            }

            var endPart = new GeometrySegment(key.Parent, value.Values.Last(), key.SegmentEnds[1]);
            Segments.Add(endPart);
        }
        
        UpdatePoints();
    }

    public ContainmentType IsCompletelyContains(MeshContour otherContour)
    {
        var intersects = false;
        
        for (var i = 0; i < Segments.Count; i++)
        {
            for (var j = 0; j < otherContour.Segments.Count; j++)
            {
                if (Collision2D.SegmentSegmentIntersection(Segments[i], otherContour.Segments[j], out var point))
                {
                    intersects = true;
                    break;
                }
            }

            if (intersects)
            {
                break;
            }
        }

        return intersects ? ContainmentType.Intersects : ContainmentType.Contains;
    }

    public MeshContour Copy()
    {
        var copy = new MeshContour();

        if (Segments != null) copy.Segments = new List<GeometrySegment>(Segments);
        copy.Points = new Vector2[Points.Length];
        Array.Copy(Points, copy.Points, Points.Length);
        if (GeometryPoints != null) copy.GeometryPoints = new List<GeometryIntersection>(GeometryPoints);
        copy.Name = Name;
        copy.IsGeometryClosed = IsGeometryClosed;
        copy.BoundingBox = BoundingBox;

        return copy;
    }
}