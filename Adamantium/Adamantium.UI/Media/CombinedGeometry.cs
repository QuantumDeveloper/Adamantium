using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
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
        // TODO: define is this suitable calculation mechanism or we need to fully process geometry first?
        Rect rect = new Rect();
        if (Geometry1 != null)
        {
            Geometry1.RecalculateBounds();
            rect = Geometry1.Bounds;
        }

        if (Geometry2 != null)
        {
            Geometry2.RecalculateBounds();
            rect = rect.Merge(Geometry2.Bounds);
        }
        
        if (Transform != null)
        {
            var matrix = Transform.Matrix;
            rect.TransformToAABB(matrix);
        }

        bounds = rect;
    }

    protected internal override void ProcessGeometryCore(GeometryType geometryType)
    {
        Geometry1?.ProcessGeometry(GeometryType.Outlined);
        Geometry2?.ProcessGeometry(GeometryType.Outlined);

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
                xorPolygon.AddContour(contour.Copy());
            }

            var xorTriangulated = xorPolygon.FillIndirect(geometryType != GeometryType.Outlined);

            // Triangulate only if geometry type is not Outlined
            if (geometryType == GeometryType.Outlined) return;

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

            if (arguableSegments.Count > 0)
            {
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
                
                OutlineMesh1.UpdateContoursPoints();
                OutlineMesh2.UpdateContoursPoints();
            }

            // 1. Merge all segments of all contours of all meshes.
            var allSegments = OutlineMesh1.MergeContourSegments();
            allSegments.AddRange(OutlineMesh2.MergeContourSegments());

            var allSegmentsDict = allSegments.ToDictionary(x => x);
            
            // 2. Form contours
            var strokeContours = FormStrokeContours(allSegmentsDict, out var onePointJointCase);
            List<GeometrySegment> triangulatorSegments = null;
            List<GeometryIntersection> triangulatorPoints = null;
            foreach (var strokeContour in strokeContours)
            {
                Mesh.AddContour(strokeContour);
            }

            // Triangulate only if geometry type is not Outlined
            if (geometryType == GeometryType.Outlined) return;

            if (onePointJointCase)
            {
                allSegmentsDict = allSegments.ToDictionary(x => x);

                triangulatorSegments = FormTriangulatorContours(allSegmentsDict);
                
                var pointsHashSet = new HashSet<GeometryIntersection>();
                triangulatorPoints = new List<GeometryIntersection>();

                foreach (var segment in triangulatorSegments)
                {
                    foreach (var end in segment.SegmentEnds)
                    {
                        if (!pointsHashSet.Contains(end))
                        {
                            pointsHashSet.Add(end);
                            triangulatorPoints.Add(end);
                        }
                    }
                }
            }

            var mergedContourPoints = onePointJointCase ? triangulatorPoints : Mesh.MergeGeometryContourPoints();
            var mergedContourSegments = onePointJointCase ? triangulatorSegments : Mesh.MergeContourSegments();

            var polygon = new Polygon(FillRule.NonZero);
            
            var triangulated = polygon.FillDirect(mergedContourPoints, mergedContourSegments);
            Mesh.SetPoints(triangulated);
        }
        else
        {
            foreach (var contour1 in OutlineMesh1.Contours)
            {
                Mesh.AddContour(contour1);
            }
            
            foreach (var contour2 in OutlineMesh2.Contours)
            {
                Mesh.AddContour(contour2);
            }

            // Triangulate only if geometry type is not Outlined
            if (geometryType == GeometryType.Outlined) return;

            var mergedContourPoints = Mesh.MergeGeometryContourPoints();
            var mergedContourSegments = Mesh.MergeContourSegments();

            var polygon = new Polygon(FillRule.NonZero);
            
            var triangulated = polygon.FillDirect(mergedContourPoints, mergedContourSegments);
            Mesh.SetPoints(triangulated);
        }
    }

    // Triangulator needs to maintain segment-intersection-segment connection, so we deal with one point join case as with single contour
    private List<GeometrySegment> FormTriangulatorContours(Dictionary<GeometrySegment, GeometrySegment> mergedSegments)
    {
        var triangulatorSegments = new List<GeometrySegment>();
        
        while (mergedSegments.Count > 0)
        {
            var currentSegment = mergedSegments.First().Value;
            var currentPoint = currentSegment.SegmentEnds[0];

            do
            {
                triangulatorSegments.Add(currentSegment);
                currentSegment.IsAlreadyInTriangulatorContour = true;

                mergedSegments.Remove(currentSegment);

                currentPoint = currentSegment.GetOtherEnd(currentPoint);
                currentSegment = currentPoint.ConnectedSegments.Count > 2 ?
                                 currentPoint.GetSegmentFromOtherParent(currentSegment) :
                                 currentPoint.GetAnyOtherSegment(currentSegment);
            } while (currentSegment != null);
        }

        return triangulatorSegments;
    }

    // We cannot provide one point join case as single contour for stroke generating, so we split for two separate contours with one of the corners connected only visually, not logically
    private List<List<GeometrySegment>> FormStrokeContours(Dictionary<GeometrySegment, GeometrySegment> mergedSegments, out bool onePointJointCase)
    {
        var strokeContours = new List<List<GeometrySegment>>();
        
        onePointJointCase = false;

        while (mergedSegments.Count > 0)
        {
            var strokeContour = new List<GeometrySegment>();

            var startSegment = mergedSegments.First().Value;
            var currentPoint = startSegment.SegmentEnds[0];
            var currentSegment = startSegment;

            var intersectionsList = new Dictionary<Vector2, GeometryIntersection>();

            do
            {
                // check and create (if needed) the new instance of GeometryIntersection for start of the new segment
                var newStart = currentPoint.Coordinates;

                if (!intersectionsList.ContainsKey(newStart))
                {
                    intersectionsList[newStart] = new GeometryIntersection(newStart);
                }

                // check and create (if needed) the new instance of GeometryIntersection for end of the new segment
                currentPoint = currentSegment.GetOtherEnd(currentPoint);
                var newEnd = currentPoint.Coordinates;

                if (!intersectionsList.ContainsKey(newEnd))
                {
                    intersectionsList[newEnd] = new GeometryIntersection(newEnd);
                }

                // create and store new segment
                var newSegment = new GeometrySegment(currentSegment.Parent, intersectionsList[newStart],
                    intersectionsList[newEnd]);
                strokeContour.Add(newSegment);

                // get next segment
                GeometrySegment nextSegment = null;
                if (currentPoint.ConnectedSegments.Count > 2)
                {
                    onePointJointCase = true;
                    nextSegment = currentPoint.GetSegmentFromSameParent(currentSegment);
                }
                else
                {
                    nextSegment = currentPoint.GetAnyOtherSegment(currentSegment);
                }

                mergedSegments.Remove(currentSegment);
                
                // switch to next segment
                currentSegment = nextSegment;
            } while (currentSegment != startSegment);

            strokeContours.Add(strokeContour);
        }

        return strokeContours;
    }

    private bool CheckGeometryBoundingBoxesIntersection()
    {
        if (Geometry1 is null || Geometry2 is null) return false;
        
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