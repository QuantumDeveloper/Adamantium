using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;
using Polygon = Adamantium.Mathematics.Polygon;

namespace Adamantium.UI.Media;

public class CombinedGeometry : Geometry
{
    private Rect bounds;
    private Mesh OutlineMesh1;
    private Mesh OutlineMesh2;
    private Rect bounds1;
    private Rect bounds2;

    public CombinedGeometry()
    {
        IsClosed = true;
    }

    public override Rect Bounds => bounds;
        
    public override Geometry Clone()
    {
        throw new System.NotImplementedException();
    }

    public static readonly AdamantiumProperty Geometry1Property =
        AdamantiumProperty.Register(nameof(Geometry1), typeof(Geometry), typeof(CombinedGeometry),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, Geometry1Changed));
        
    public static readonly AdamantiumProperty Geometry2Property =
        AdamantiumProperty.Register(nameof(Geometry2), typeof(Geometry), typeof(CombinedGeometry),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, Geometry2Changed));
        
    public static readonly AdamantiumProperty GeometryCombineModeProperty =
        AdamantiumProperty.Register(nameof(GeometryCombineMode), typeof(GeometryCombineMode), typeof(CombinedGeometry),
            new PropertyMetadata(GeometryCombineMode.Union, PropertyMetadataOptions.AffectsRender, CombineModeChanged));

    private static void Geometry1Changed(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
            
    }
        
    private static void Geometry2Changed(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is CombinedGeometry combined)
        {
            if (e.OldValue is Geometry geometry1) geometry1.ComponentUpdated -= combined.GeometryOnComponentUpdated;

            if (e.NewValue is Geometry geometry2) geometry2.ComponentUpdated += combined.GeometryOnComponentUpdated;
        }
    }

    private void GeometryOnComponentUpdated(object? sender, ComponentUpdatedEventArgs e)
    {
        InvalidateGeometry();
    }

    private static void CombineModeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
            
    }

    public Geometry Geometry1
    {
        get => GetValue<Geometry>(Geometry1Property);
        set => SetValue(Geometry1Property, value);
    }
        
    public Geometry Geometry2
    {
        get => GetValue<Geometry>(Geometry2Property);
        set => SetValue(Geometry2Property, value);
    }
        
    public GeometryCombineMode GeometryCombineMode
    {
        get => GetValue<GeometryCombineMode>(GeometryCombineModeProperty);
        set => SetValue(GeometryCombineModeProperty, value);
    }

    private readonly List<Vector2> selfIntersectedPoints = new ();
    private readonly List<LineSegment2D> mergedSegments = new ();
    private readonly List<Vector2> mergedPoints = new ();
    private readonly HashSet<Vector2> mergedPointsHash = new ();

    public override void RecalculateBounds()
    {
        var points = OutlineMesh.MergeContourPoints();
        bounds = Rect.FromPoints(points);
    }

    protected internal override void ProcessGeometryCore()
    {
        Geometry1.InvalidateGeometry();
        Geometry1?.ProcessGeometry();
        Geometry2?.ProcessGeometry();
            
        selfIntersectedPoints.Clear();
        mergedSegments.Clear();
        mergedPoints.Clear();
        mergedPointsHash.Clear();
        Mesh.Clear();
        OutlineMesh.Clear();
        
        // OutlineMesh1?.Clear();
        // OutlineMesh2?.Clear();

        if (Geometry1 != null)
        {
            OutlineMesh1 = Geometry1.OutlineMesh;
            OutlineMesh1.SplitContoursOnSegments();
            bounds1 = Geometry1.Bounds;
        }
        else
            OutlineMesh1 = new Mesh();

        if (Geometry2 != null)
        {
            OutlineMesh2 = Geometry2.OutlineMesh;
            OutlineMesh2.SplitContoursOnSegments();
            bounds2 = Geometry2.Bounds;
        }
        else
            OutlineMesh2 = new Mesh();
            
        if (CheckGeometryIntersections())
        {
            MergePoints();
            MergeSegments();
                
            FindEdgeIntersectionPointsExtended(OutlineMesh1, OutlineMesh2, out var edgePoints);
                
            for (var k = 0; k < edgePoints.Count; ++k)
            {
                if (!selfIntersectedPoints.Contains(edgePoints[k]))
                {
                    selfIntersectedPoints.Add(edgePoints[k]);
                }
            }
                
            //UpdatePolygonUsingIntersectionPoints(edgePoints);
                
            if (GeometryCombineMode == GeometryCombineMode.Union)
            {
                var intersections = FindUnionPoints(OutlineMesh1, OutlineMesh2);
                var intersections2 = FindUnionPoints(OutlineMesh2, OutlineMesh1);
                UpdateMeshContoursUsingInterPoints(edgePoints, OutlineMesh1);
                UpdateMeshContoursUsingInterPoints(edgePoints, OutlineMesh2);
                
                // intersections.AddRange(edgePoints);
                // intersections2.AddRange(edgePoints);
                
                RemoveInternalSegments(intersections, OutlineMesh2);
                RemoveInternalSegments(intersections2, OutlineMesh1);

                var polygon = new Polygon();
                foreach (var contour in OutlineMesh1.Contours)
                {
                    var polygonItem = new PolygonItem(contour.Points);
                    polygon.AddItem(polygonItem);
                    OutlineMesh.AddContour(contour.Points, contour.IsGeometryClosed);
                }

                foreach (var contour in OutlineMesh2.Contours)
                {
                    var polygonItem = new PolygonItem(contour.Points);
                    polygon.AddItem(polygonItem);
                    OutlineMesh.AddContour(contour.Points, contour.IsGeometryClosed);
                }
                polygon.FillRule = FillRule.NonZero;
                var points = polygon.Fill();
                Mesh.SetPoints(points).Optimize();
                mergedSegments.Clear();
                mergedSegments.AddRange(OutlineMesh.MergeContourSegments());
            }
            else if (GeometryCombineMode == GeometryCombineMode.Intersect)
            {
                var intersections = FindAllPossibleIntersections(OutlineMesh1, OutlineMesh2, edgePoints);
                var segments = FindIntersectedSegments(intersections);
                mergedPoints.Clear();
                mergedPoints.AddRange(intersections);
                mergedSegments.Clear();
                mergedSegments.AddRange(segments);
            }
            else if (GeometryCombineMode == GeometryCombineMode.Exclude)
            {
                var intersections = FindGeometryIntersections(OutlineMesh1, OutlineMesh2, edgePoints);
                    
                var pointsToRemove = new List<Vector2>();
                foreach (var meshPoint in OutlineMesh2.Points)
                {
                    pointsToRemove.Add((Vector2)meshPoint);
                }

                var intersections2 = FindGeometryIntersections(OutlineMesh2, OutlineMesh1,
                    new List<Vector2>());
                pointsToRemove.AddRange(intersections2);
                pointsToRemove.AddRange(edgePoints);
                var diff = intersections.Except(edgePoints).ToArray();
                foreach (var pt in diff)
                {
                    pointsToRemove.Remove(pt);
                }

                RemoveInternalSegments(pointsToRemove);
            }
            else if (GeometryCombineMode == GeometryCombineMode.Xor)
            {
                var polygon = new Polygon();
                foreach (var meshLayer in OutlineMesh1.Contours)
                {
                    var polygonItem = new PolygonItem(meshLayer.Points);
                    polygon.AddItem(polygonItem);
                }
                polygon.FillRule = FillRule.EvenOdd;
                var points = polygon.Fill();
                Mesh.SetPoints(points).Optimize();
                    
                var ordered = PolygonHelper.OrderSegments(polygon.MergedSegments);
                var orderedPoints = new List<Vector3>();
                foreach (var segment2D in ordered)
                {
                    orderedPoints.Add((Vector3)segment2D.Start);
                }
                OutlineMesh.AddContour(orderedPoints, IsClosed);
                if (polygon.MergedSegments.Count != 0)
                {
                    orderedPoints.Clear();
                    foreach (var segment2D in ordered)
                    {
                        orderedPoints.Add((Vector3)segment2D.Start);
                    }
                        
                    OutlineMesh.AddContour(orderedPoints, IsClosed);
                }
            }
        }
            
        // var points1 = OutlineMesh1.Points.ToList();
        // points1.Add(points1[0]);
        // var points2 = OutlineMesh2.Points.ToList();
        // points2.Add(points2[0]);
        // points1.AddRange(points2);
        //OutlineMesh.SetPoints(points1);


        // var pts = new List<Vector2>();
        // foreach (var p in points1)
        // {
        //     pts.Add((Vector2)p);
        // }
            
        if (GeometryCombineMode != GeometryCombineMode.Xor && GeometryCombineMode != GeometryCombineMode.Union)
        {
            var ordered = PolygonHelper.OrderSegments(mergedSegments);
            mergedPoints.Clear();
            foreach (var segment in ordered)
            {
                mergedPoints.Add(segment.Start);
            }
            OutlineMesh.AddContour(Utilities.ToVector3(mergedPoints), IsClosed);
            var points = new List<Vector2>(mergedPoints);
                
            mergedPoints.Clear();
            foreach (var segment in mergedSegments)
            {
                mergedPoints.Add(segment.Start);
            }
            points.AddRange(mergedPoints);
                
            OutlineMesh.AddContour(Utilities.ToVector3(mergedPoints), IsClosed);
            var polygon1 = new Polygon();        
            polygon1.FillRule = FillRule.NonZero;
            ordered.AddRange(mergedSegments);
            var triangulated = polygon1.FillDirect(points, ordered);
            Mesh.SetPoints(triangulated);
        }
            
        // var polygon1 = new Polygon();
        // polygon1.FillRule = FillRule.NonZero;
        // //var points1 = polygon1.FillDirect(pts, mergedSegments);
        // Mesh.SetPoints(points1).Optimize();
        // //Mesh.SetPoints(mergedPoints);
        // //Mesh.SetTopology(PrimitiveType.LineStrip);
        //
        // // TODO: fix issue with merged segments/points for XOR combine mode
        // OutlineMesh = new Mesh();
        // OutlineMesh.SetPoints(points1);
    }

    private void MergePoints()
    {
        mergedPoints.Clear();
        foreach (var point in OutlineMesh1.Points)
        {
            var p = (Vector2)point;
            mergedPoints.Add(p);
        }
            
        foreach (var point in OutlineMesh2.Points)
        {
            var p = (Vector2)point;
            mergedPoints.Add(p);
        }
    }
        
    private void MergeSegments()
    {
        mergedSegments.Clear();
        mergedSegments.AddRange(OutlineMesh1.MergeContourSegments());
        mergedSegments.AddRange(OutlineMesh2.MergeContourSegments());
    }
        
    private bool CheckGeometryIntersections()
    {
        var intersects = bounds1.Intersects(bounds2);
            
        return intersects;
    }
    
    private bool FindEdgeIntersectionPointsExtended(Mesh mesh1, Mesh mesh2, out List<Vector2> edgePoints)
    {
        edgePoints = new List<Vector2>();
        var mergedSegments1 = mesh1.MergeContourSegments();
        var mergedSegments2 = mesh2.MergeContourSegments();
        foreach (var segment1 in mergedSegments1)
        {
            foreach (var segment2 in mergedSegments2)
            {
                var seg1 = segment1;
                var seg2 = segment2;
                if (Collision2D.SegmentSegmentIntersection(ref seg1, ref seg2, out var point))
                {
                    if (!edgePoints.Contains(point))
                    {
                        edgePoints.Add(point);
                    }
                }
            }
        }
        
        return edgePoints.Count > 0;
    }
        
    // private bool FindEdgeIntersectionPoints(Mesh mesh1, Mesh mesh2, out List<Vector2> edgePoints)
    // {
    //     edgePoints = new List<Vector2>();
    //     for (var i = 0; i < mesh1.Segments.Length; i++)
    //     {
    //         for (var j = 0; j < mesh2.Segments.Length; j++)
    //         {
    //             var segment1 = mesh1.Segments[i];
    //             var segment2 = mesh2.Segments[j];
    //             if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out var point))
    //             {
    //                 if (!edgePoints.Contains(point))
    //                 {
    //                     edgePoints.Add(point);
    //                 }
    //             }
    //         }
    //     }
    //
    //     return edgePoints.Count > 0;
    // }
        
    /// <summary>
    /// Updates segments for polygons using self Intersection points
    /// </summary>
    /// <param name="intersectionPoints">Self intersection points</param>
    private void UpdatePolygonUsingIntersectionPoints(List<Vector2> intersectionPoints)
    {
        if (intersectionPoints.Count == 0)
        {
            return;
        }

        foreach (var contour in OutlineMesh1.Contours)
        {
            UpdateContourBasedOnIntersectionPoints(intersectionPoints, contour);
        }
        
        foreach (var contour in OutlineMesh2.Contours)
        {
            UpdateContourBasedOnIntersectionPoints(intersectionPoints, contour);
        }
    }

    private void UpdateContourBasedOnIntersectionPoints(List<Vector2> intersectionPoints, MeshContour contour)
    {
        var tempSegments = new List<LineSegment2D>(contour.Segments);
        for (var i = 0; i < intersectionPoints.Count; i++)
        {
            var interPoint = intersectionPoints[i];
            if (!PolygonHelper.GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
            for (var j = 0; j < segments.Count; j++)
            {
                var segment = segments[j];
                    
                if (MathHelper.WithinEpsilon(interPoint, segment.Start, Polygon.Epsilon) ||
                    MathHelper.WithinEpsilon(interPoint, segment.End, Polygon.Epsilon))
                {
                    continue;
                }

                var index = tempSegments.IndexOf(segment);
                tempSegments.RemoveAt(index);
                var seg1 = new LineSegment2D(segment.Start, interPoint);
                if (PolygonHelper.InsertSegment(tempSegments, seg1, index))
                {
                    index++;
                }

                var seg2 = new LineSegment2D(interPoint, segment.End);
                PolygonHelper.InsertSegment(tempSegments, seg2, index);
            }
        }
        
        UpdateContourPoints(tempSegments, contour);
    }
        
    /// <summary>
    /// Remove all segments based on intersection points in case of Non-Zero rule
    /// </summary>
    /// <param name="interPoints"></param>
    private void RemoveInternalSegments(List<Vector2> interPoints)
    {
        for (var i = 0; i < interPoints.Count; i++)
        {
            for (var j = 0; j < interPoints.Count; j++)
            {
                var point1 = interPoints[i];
                var point2 = interPoints[j];
                if (MathHelper.WithinEpsilon(point1, point2, Polygon.Epsilon))
                { continue; }

                if (PolygonHelper.IsConnectedInvariant(point1, point2, mergedSegments, out var segment2D))
                {
                    mergedSegments.Remove(segment2D);
                }
            }
        }
        UpdateMergedPoints();
    }
    
    private void UpdateMeshContoursUsingInterPoints(List<Vector2> intersectionPoints, Mesh mesh)
    {
        foreach (var contour in mesh.Contours)
        {
            var tempSegments = contour.Segments.ToList();
            foreach (var interPoint in intersectionPoints)
            {
                if (!PolygonHelper.GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
                for (var j = 0; j < segments.Count; j++)
                {
                    var segment = segments[j];
                    
                    if (MathHelper.WithinEpsilon(interPoint, segment.Start, Polygon.Epsilon) ||
                        MathHelper.WithinEpsilon(interPoint, segment.End, Polygon.Epsilon))
                    {
                        continue;
                    }

                    var index = tempSegments.IndexOf(segment);
                    tempSegments.RemoveAt(index);
                    var seg1 = new LineSegment2D(segment.Start, interPoint);
                    if (PolygonHelper.InsertSegment(tempSegments, seg1, index))
                    {
                        index++;
                    }

                    var seg2 = new LineSegment2D(interPoint, segment.End);
                    PolygonHelper.InsertSegment(tempSegments, seg2, index);
                }
            }
        
            UpdateContourPoints(tempSegments, contour);
        }
    }
    
    private void RemoveInternalSegments(List<Vector2> interPoints, Mesh mesh)
    {
        var newContours = new List<MeshContour>(); 
        foreach (var contour in mesh.Contours)
        {
            var segments = contour.Segments.ToList();
            foreach (var interPoint in interPoints)
            {
                if (PolygonHelper.GetSegmentsFromPoint(segments, interPoint, out var foundSegments))
                {
                    foreach (var segment in foundSegments)
                    {
                        segments.Remove(segment);
                    }
                }
            }

            var segmentGroups = PolygonHelper.GetSegmentGroups(segments);

            foreach (var group in segmentGroups)
            {
                if (group.Segments.Count <= 1) continue;
                var newContour = new MeshContour();
                newContour.IsGeometryClosed = contour.IsGeometryClosed;
                UpdateContourPoints(group.Segments, newContour);
                newContours.Add(newContour);
            }
        }

        mesh.Contours.Clear();
        mesh.AddContours(newContours);
    }
    
    private List<LineSegment2D> FindIntersectedSegments(List<Vector2> interPoints)
    {
        var segments = new List<LineSegment2D>();
        for (var i = 0; i < interPoints.Count; i++)
        {
            for (var j = 0; j < interPoints.Count; j++)
            {
                var point1 = interPoints[i];
                var point2 = interPoints[j];
                if (MathHelper.WithinEpsilon(point1, point2, Polygon.Epsilon))
                { continue; }

                if (PolygonHelper.IsConnectedInvariant(point1, point2, mergedSegments, out var segment2D))
                {
                    mergedSegments.Remove(segment2D);
                    segments.Add(segment2D);
                }
            }
        }
        //UpdateMergedPoints();
        return segments;
    }
        
    private List<Vector2> FindUnionPoints(Mesh mesh1, Mesh mesh2)
    {
        var interPoints = new List<Vector2>();
        var mesh1Segments = mesh1.MergeContourSegments();
        var mesh2Points = mesh2.MergeContourPoints();

        for (var k = 0; k < mesh2Points.Count; k++)
        {
            var currentPoint = (Vector2)mesh2Points[k];
            var ray = new Ray2D(new Vector2(bounds1.X, currentPoint.Y), Vector2.UnitX);
            var localPoints = new List<Vector2>();
            for (var i = 0; i < mesh1Segments.Count; i++)
            {
                var segment = mesh1Segments[i];
                if (Collision2D.RaySegmentIntersection(ref ray, ref segment, out Vector2 point))
                {
                    localPoints.Add(Vector2.Round(point, Mesh.RoundPrecision));
                }
            }
            localPoints.Add(currentPoint);

            localPoints.Sort(HorizontalPointsComparer.Default);
            for (int l = 0; l < localPoints.Count; l++)
            {
                if (localPoints[l] == currentPoint && l % 2 != 0)
                {
                    interPoints.Add(currentPoint);
                    break;
                }
            }
        }

        return interPoints;
    }
    
    private class HorizontalPointsComparer : IComparer<Vector2>
    {
        public static HorizontalPointsComparer Default => new HorizontalPointsComparer();

        public int Compare(Vector2 x, Vector2 y)
        {
            if (MathHelper.WithinEpsilon(x.X, y.X, MathHelper.ZeroToleranceD))
            {
                return 0;
            }

            return x.X < y.X ? -1 : 1;
        }
    }
        
    /// <summary>
    /// Find all possible intersections between 2 polygons including intersection points and remove all merged segments 
    /// if any of 2 points are connected as segments. This needs for Non-zero rule to remove all inner segments between 2 polygons to correctly triangulate them
    /// </summary>
    /// <param name="mesh1">first polygon</param>
    /// <param name="mesh2">second polygon</param>
    /// <param name="edgePoints">intersection points between 2 polygons, which were found earlier on previous step</param>
    /// <returns>Collection of intersection points, including points, which is not lying on segments, but also inside polygons.</returns>
    /// <remarks>During running this method, it will remove all merged segments from <see cref="Polygon"/> if some of its segments contains both intersection points</remarks>
    private List<Vector2> FindAllPossibleIntersections(Mesh mesh1, Mesh mesh2, List<Vector2> edgePoints)
    {
        var allIntersections = new List<Vector2>(edgePoints);
        var contourPoints1 = mesh1.MergeContourPoints();
        var contourPoints2 = mesh2.MergeContourPoints();

        var mergedSegments1 = mesh1.MergeContourSegments();
        var mergedSegments2 = mesh2.MergeContourSegments();
        
        foreach (var p in contourPoints1)
        {
            var point = (Vector2)p;
            if (Collision2D.IsPointInsideArea(point, mergedSegments2) &&
                !allIntersections.Contains(point))
            {
                allIntersections.Add(point);
            }
        }
        foreach (var p in contourPoints2)
        {
            var point = (Vector2)p;
            if (Collision2D.IsPointInsideArea(point, mergedSegments1) &&
                !allIntersections.Contains(point))
            {
                allIntersections.Add(point);
            }
        }

        return allIntersections;
    }
        
    private List<Vector2> FindGeometryIntersections(Mesh mesh1, Mesh mesh2, List<Vector2> edgePoints)
    {
        var allIntersections = new List<Vector2>(edgePoints);
        var mesh1Segments = mesh1.MergeContourSegments();
        var mesh2Points = mesh2.MergeContourPoints();
        
        for (var i = 0; i < mesh2Points.Count; i++)
        {
            var point = (Vector2)mesh2Points[i];
            if (Collision2D.IsPointInsideArea(point, mesh1Segments) &&
                !allIntersections.Contains(point))
            {
                allIntersections.Add(point);
            }
        }

        return allIntersections;
    }
    
    private void UpdateContourPoints(IEnumerable<LineSegment2D> segments, MeshContour contour)
    {
        var pointsHash = new HashSet<Vector2>();
        var points = new List<Vector2>();
        foreach (var segment in segments)
        {
            if (!pointsHash.Contains(segment.Start))
            {
                points.Add(segment.Start);
                pointsHash.Add(segment.Start);
            }

            if (!pointsHash.Contains(segment.End))
            {
                points.Add(segment.End);
                pointsHash.Add(segment.End);
            }
        }
        contour.SetOrUpdatePoints(points);
    }


    private void UpdateMergedPoints()
    {
        mergedPoints.Clear();
        mergedPointsHash.Clear();
        for (var i = 0; i < mergedSegments.Count; ++i)
        {
            var segment = mergedSegments[i];
            if (!mergedPointsHash.Contains(segment.Start))
            {
                mergedPoints.Add(segment.Start);
                mergedPointsHash.Add(segment.Start);
            }

            if (!mergedPointsHash.Contains(segment.End))
            {
                mergedPoints.Add(segment.End);
                mergedPointsHash.Add(segment.End);
            }
        }
    }

    private void UpdateContourPoints(List<LineSegment2D> segments, MeshContour contour)
    {
        var hash = new HashSet<Vector2>();
        var points = new List<Vector2>();
        foreach (var segment in segments)
        {
            if (!hash.Contains(segment.Start))
            {
                points.Add(segment.Start);
                hash.Add(segment.Start);
            }

            if (!hash.Contains(segment.End))
            {
                points.Add(segment.End);
                hash.Add(segment.End);
            }
        }

        contour.SetOrUpdatePoints(points);
    }
    
    // private void UpdateMeshPoints(Mesh mesh)
    // {
    //     var hash = new HashSet<Vector2>();
    //     var points = new List<Vector2>();
    //     foreach (var segment in mesh.Segments)
    //     {
    //         if (!hash.Contains(segment.Start))
    //         {
    //             points.Add(segment.Start);
    //             hash.Add(segment.Start);
    //         }
    //
    //         if (!hash.Contains(segment.End))
    //         {
    //             points.Add(segment.End);
    //             hash.Add(segment.End);
    //         }
    //     }
    //
    //     mesh.SetPoints(points);
    // }
}