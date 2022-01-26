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
        var points = Mesh.MergeContourPoints();
        bounds = Rect.FromPoints(points);
    }

    protected internal override void ProcessGeometryCore()
    {
        Geometry1?.InvalidateGeometry();
        Geometry1?.ProcessGeometry();
        Geometry2?.ProcessGeometry();

        Mesh.Clear();

        if (GeometryCombineMode == GeometryCombineMode.Xor)
        {
            if (Geometry1 != null) Mesh.Contours.AddRange(Geometry1.Mesh.Contours);
            if (Geometry2 != null) Mesh.Contours.AddRange(Geometry2.Mesh.Contours);
            
            var xorPolygon = new Polygon
            {
                FillRule = FillRule.EvenOdd
            };

            foreach (var contour in Mesh.Contours)
            {
                xorPolygon.AddItem(contour.Copy());
            }

            var xorTriangulated = xorPolygon.Fill();
            Mesh.SetPoints(xorTriangulated);

            return;
        }
        
        if (Geometry1 is {IsClosed: true})
        {
            OutlineMesh1 = Geometry1.Mesh;
            OutlineMesh1.SplitContoursOnSegments();
            bounds1 = Geometry1.Bounds;
        }
        else
            OutlineMesh1 = new Mesh();

        if (Geometry2 is {IsClosed: true})
        {
            OutlineMesh2 = Geometry2.Mesh;
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

            // mark all segments as inner, outer or arguable (border case) relatively to other mesh
            var arguableSegments = MarkSegments(OutlineMesh1, OutlineMesh2);
            arguableSegments.AddRange(MarkSegments(OutlineMesh2, OutlineMesh1));

            if (arguableSegments.Count > 0)
            {
                // resolve arguable segments as inner or outer
                var mergedSegments = OutlineMesh1.MergeContourSegments();
                mergedSegments.AddRange(OutlineMesh2.MergeContourSegments());

                ContourProcessingHelper.ResolveArguableSegments(arguableSegments, mergedSegments);
            }

            // remove only inner / outer segments (according to mode), arguable segments will be skipped
            RemoveSegmentsByMode();

            // additionally remove resolved segment if needed (according to mode)
            foreach (var arguableSeg in arguableSegments)
            {
                switch (GeometryCombineMode)
                {
                    case GeometryCombineMode.Union:
                        if (arguableSeg.IsInner)
                        {
                            arguableSeg.RemoveSelfFromConnectedSegments();
                            arguableSeg.RemoveSelfFromParent();
                        }

                        break;
                    case GeometryCombineMode.Exclude:
                        if (!arguableSeg.IsInner)
                        {
                            arguableSeg.RemoveSelfFromConnectedSegments();
                            arguableSeg.RemoveSelfFromParent();
                        }

                        break;
                }
            }

            // 1. Merge all points of all contours of all meshes.
            // If point is already present, merge connected segments for it.

            var mesh1Points = OutlineMesh1.MergeGeometryContourPoints();
            var mesh2Points = OutlineMesh2.MergeGeometryContourPoints();

            var mesh1PointDict = mesh1Points.ToDictionary(x => x.Coordinates);

            foreach (var point in mesh2Points)
            {
                if (!mesh1PointDict.ContainsKey(point.Coordinates))
                {
                    mesh1PointDict.Add(point.Coordinates, point);
                }
            }

            var mergedPoints = mesh1PointDict.Values.ToList();

            // 2. Form contours (we're splitting contours here, including the case of one-point joint, see below)
            /*
            _________
            |        |
            |        |
            |________|________
                     |        |
                     |        |
                     |________| 
             */

            while (mergedPoints.Count > 0)
            {
                var contourSegments = new List<GeometrySegment>();

                var startContourPoint = mergedPoints.First();
                var startSegment = startContourPoint.ConnectedSegments.First();
                var currentContourPoint = startContourPoint;
                var currentSegment = startSegment;

                var intersectionsList = new Dictionary<Vector2, GeometryIntersection>();

                do
                {
                    // check and create (if needed) the new instance of GeometryIntersection for start of the new segment
                    var newStart = currentContourPoint.Coordinates;

                    if (!intersectionsList.ContainsKey(newStart))
                    {
                        intersectionsList[newStart] = new GeometryIntersection(newStart);
                    }

                    // remove point from merged points list
                    mergedPoints.Remove(currentContourPoint);

                    // get next point
                    currentContourPoint = currentSegment.GetOtherEnd(currentContourPoint);

                    // check and create (if needed) the new instance of GeometryIntersection for end of the new segment
                    var newEnd = currentContourPoint.Coordinates;

                    if (!intersectionsList.ContainsKey(newEnd))
                    {
                        intersectionsList[newEnd] = new GeometryIntersection(newEnd);
                    }

                    // create and store new segment
                    var newSegment = new GeometrySegment(currentSegment.Parent, intersectionsList[newStart],
                        intersectionsList[newEnd]);
                    contourSegments.Add(newSegment);

                    // get next segment
                    GeometrySegment nextSegment = null;
                    if (currentContourPoint.ConnectedSegments.Count > 2)
                        nextSegment = currentContourPoint.GetSiblingOtherSegment(currentSegment);
                    else nextSegment = currentContourPoint.GetAnyOtherSegment(currentSegment);

                    // remove current segment from lists of connected segments of it's ends (in order not to process it anymore further)
                    currentSegment.RemoveSelfFromConnectedSegments();

                    // switch to next segment
                    currentSegment = nextSegment;
                } while (currentContourPoint != startContourPoint);

                Mesh.AddContour(contourSegments);
            }

            var mergedContourPoints = Mesh.MergeGeometryContourPoints();
            var mergedContourSegments = Mesh.MergeContourSegments();

            var polygon = new Polygon
            {
                FillRule = FillRule.NonZero
            };

            var triangulated = polygon.FillDirect(mergedContourPoints, mergedContourSegments);
            Mesh.SetPoints(triangulated);
        }
        else
        {
            var polygon = new Polygon
            {
                FillRule = FillRule.NonZero
            };
            
            foreach (var contour1 in OutlineMesh1.Contours)
            {
                polygon.AddItem(contour1.Copy());
                Mesh.AddContour(contour1);
            }
            
            foreach (var contour2 in OutlineMesh2.Contours)
            {
                polygon.AddItem(contour2.Copy());
                Mesh.AddContour(contour2);
            }
            
            var triangulated = polygon.Fill();
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

        ContourProcessingHelper.ProcessContoursIntersections(mergedSegments1, mergedSegments2);
        
        mesh1.UpdateContoursPoints();
        mesh2.UpdateContoursPoints();
    }

    private List<GeometrySegment> MarkSegments(Mesh mesh1, Mesh mesh2)
    {
        var mesh1Segments = mesh1.MergeContourSegments();
        var mesh2Segments = mesh2.MergeContourSegments();

        return ContourProcessingHelper.MarkSegments(mesh1Segments, mesh2Segments);
    }

    private void RemoveSegmentsByMode()
    {
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
        }
    }
}