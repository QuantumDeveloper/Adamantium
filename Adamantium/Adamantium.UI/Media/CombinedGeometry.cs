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

        Mesh.Clear();
        OutlineMesh.Clear();
        
        if (Geometry1 is {IsClosed: true})
        {
            OutlineMesh1 = Geometry1.OutlineMesh;
            OutlineMesh1.SplitContoursOnSegments();
            bounds1 = Geometry1.Bounds;
        }
        else
            OutlineMesh1 = new Mesh();

        if (Geometry2 is {IsClosed: true})
        {
            OutlineMesh2 = Geometry2.OutlineMesh;
            OutlineMesh2.SplitContoursOnSegments();
            bounds2 = Geometry2.Bounds;
        }
        else
            OutlineMesh2 = new Mesh();
            
        // TODO check case if 'null' (or default in other words) bounding box is inside the real one - will it count as intersection?
        if (CheckGeometryBoundingBoxesIntersection())
        {
            // find all intersections and break all intersected segments on 2 parts
            ProcessOutlinesIntersections(OutlineMesh1, OutlineMesh2);

            // mark all segments as inner or outer relatively to other mesh
            MarkSegments(OutlineMesh1, OutlineMesh2);
            MarkSegments(OutlineMesh2, OutlineMesh1);

            switch (GeometryCombineMode)
            {
                case GeometryCombineMode.Union:
                    OutlineMesh1.RemoveSegmentsByRule(true);
                    OutlineMesh2.RemoveSegmentsByRule(true);
                    break;
                case GeometryCombineMode.Intersect:
                    OutlineMesh1.RemoveSegmentsByRule(false);
                    OutlineMesh2.RemoveSegmentsByRule(false);
                    break;
                case GeometryCombineMode.Exclude:
                    OutlineMesh1.RemoveSegmentsByRule(true);
                    OutlineMesh2.RemoveSegmentsByRule(false);
                    break;
                case GeometryCombineMode.Xor:
                    OutlineMesh.Contours.AddRange(OutlineMesh1.Contours);
                    OutlineMesh.Contours.AddRange(OutlineMesh2.Contours);
                    break;
            }
        }

        if (GeometryCombineMode == GeometryCombineMode.Xor)
        {
            // EVEN ODD
            var polygon = new Polygon
            {
                FillRule = FillRule.EvenOdd
            };

            foreach (var contour in OutlineMesh.Contours)
            {
                polygon.AddItem(contour.Copy());
            }
            
            var triangulated = polygon.Fill();
            Mesh.SetPoints(triangulated);
        }
        else
        {
            // 1. Merge all points of all contours of all meshes.
            // If point is already present, merge connected segments for it.

            var mesh1Points = OutlineMesh1.MergeGeometryContourPoints();
            var mesh2Points = OutlineMesh2.MergeGeometryContourPoints();

            var mesh1PointDict = mesh1Points.ToDictionary(x => x);
            
            foreach (var point in mesh2Points)
            {
                if (mesh1PointDict.ContainsKey(point))
                {
                    mesh1PointDict[point].ConnectedSegments.AddRange(point.ConnectedSegments);
                }
                else
                {
                    mesh1PointDict.Add(point, point);
                }
            }

            var mergedPoints = mesh1PointDict.Values.ToList();
            
            // 2. Form contours

            while (mergedPoints.Count > 0)
            {
                var contourSegments = new List<GeometrySegment>();

                var startContourPoint = mergedPoints.First();
                var startSegment = startContourPoint.ConnectedSegments.First();
                var currentContourPoint = startContourPoint;
                var currentSegment = startSegment;
                
                do
                {
                    contourSegments.Add(currentSegment);
                    mergedPoints.Remove(currentContourPoint);

                    currentContourPoint = currentSegment.GetOtherEnd(currentContourPoint);
                    currentSegment = currentContourPoint.GetOtherSegment(currentSegment);
                } while (currentContourPoint != startContourPoint);
                
                OutlineMesh.AddContour(contourSegments);
            }
            
            // NON ZERO
            var mergedContourPoints = OutlineMesh.MergeGeometryContourPoints();
            var mergedContourSegments = OutlineMesh.MergeContourSegments();
            
            var polygon = new Polygon();        
            polygon.FillRule = FillRule.NonZero;
            var triangulated = polygon.FillDirect(mergedContourPoints, mergedContourSegments);
            Mesh.SetPoints(triangulated);
        }
    }

    private bool CheckGeometryBoundingBoxesIntersection()
    {
        var intersects = bounds1.Intersects(bounds2);
            
        return intersects;
    }

    private void ProcessOutlinesIntersections(Mesh mesh1, Mesh mesh2)
    {
        var mergedSegments1 = mesh1.MergeContourSegments();
        var mergedSegments2 = mesh2.MergeContourSegments();

        mesh1.ClearContoursSegments();
        mesh2.ClearContoursSegments();
        
        var intersectionsList = new Dictionary<Vector2, GeometryIntersection>();
        var mesh1Intersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();
        var mesh2Intersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();
        
        foreach (var segment1 in mergedSegments1)
        {
            mesh1Intersections[segment1] = new SortedList<double, GeometryIntersection>();
            
            foreach (var segment2 in mergedSegments2)
            {
                if (!mesh2Intersections.ContainsKey(segment2))
                {
                    mesh2Intersections[segment2] = new SortedList<double, GeometryIntersection>();
                }
                
                if (Collision2D.SegmentSegmentIntersection(segment1, segment2, out var point))
                {
                    if (point != segment1.Start && point != segment1.End)
                    {
                        if (!intersectionsList.ContainsKey(point))
                        {
                            intersectionsList[point] = new GeometryIntersection(point);
                        }
                        
                        var distanceToStart = (point - segment1.Start).Length();
                        mesh1Intersections[segment1].Add(distanceToStart, intersectionsList[point]);
                    }
                    
                    if (point != segment2.Start && point != segment2.End)
                    {
                        if (!intersectionsList.ContainsKey(point))
                        {
                            intersectionsList[point] = new GeometryIntersection(point);
                        }
                        
                        var distanceToStart = (point - segment2.Start).Length();
                        mesh2Intersections[segment2].Add(distanceToStart, intersectionsList[point]);
                    }
                }
            }
        }

        FillMeshSegments(mesh1Intersections);
        FillMeshSegments(mesh2Intersections);
        
        mesh1.UpdateContoursPoints();
        mesh2.UpdateContoursPoints();
    }
    
    private void FillMeshSegments(Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>> meshIntersections)
    {
        foreach (var (key, value) in meshIntersections)
        {
            if (value.Count == 0)
            {
                key.Parent.Segments.Add(key);
                continue;
            }

            key.RemoveSelfFromConnectedSegments();

            var startPart = new GeometrySegment(key.Parent, key.SegmentEnds[0], value.Values.First());
            key.Parent.Segments.Add(startPart);

            for (var i = 0; i < value.Values.Count - 1; i++)
            {
                var int1 = value.Values[i];
                var int2 = value.Values[i + 1];

                var seg = new GeometrySegment(key.Parent, int1, int2);
                key.Parent.Segments.Add(seg);
            }

            var endPart = new GeometrySegment(key.Parent, value.Values.Last(), key.SegmentEnds[1]);
            key.Parent.Segments.Add(endPart);
        }
    }

    private void MarkSegments(Mesh mesh1, Mesh mesh2)
    {
        var mesh1Segments = mesh1.MergeContourSegments();
        var mesh2Segments = mesh2.MergeContourSegments();

        foreach (var segment2 in mesh2Segments)
        {
            var segmentCenter = (segment2.End - segment2.Start) * 0.5 + segment2.Start;
            
            var ray = new Ray2D(segmentCenter, Vector2.UnitX);
            var intersectionCnt = 0;
            var rayIntersectionPoints = new HashSet<Vector2>();
            
            foreach (var segment1 in mesh1Segments)
            {
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

            if (intersectionCnt % 2 != 0)
            {
                segment2.IsInnerSegment = true;
            }
        }
    }
}