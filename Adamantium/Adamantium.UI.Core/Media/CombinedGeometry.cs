using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using Adamantium.Mathematics.Triangulation;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.RoutedEvents;
using Polygon = Adamantium.Mathematics.Triangulation.Polygon;

namespace Adamantium.UI.Core.Media;

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
        if (a is CombinedGeometry combined)
        {
            // Was empty: Geometry1's own updates and replacement were silently ignored (asymmetric with Geometry2).
            if (e.OldValue is Geometry oldGeometry) oldGeometry.ComponentUpdated -= combined.GeometryOnComponentUpdated;
            if (e.NewValue is Geometry newGeometry) newGeometry.ComponentUpdated += combined.GeometryOnComponentUpdated;
            combined.InvalidateGeometry();
        }
    }

    private static void Geometry2Changed(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is CombinedGeometry combined)
        {
            if (e.OldValue is Geometry oldGeometry) oldGeometry.ComponentUpdated -= combined.GeometryOnComponentUpdated;
            if (e.NewValue is Geometry newGeometry) newGeometry.ComponentUpdated += combined.GeometryOnComponentUpdated;
            combined.InvalidateGeometry();
        }
    }

    private void GeometryOnComponentUpdated(object sender, ComponentUpdatedEventArgs e)
    {
        InvalidateGeometry();
    }

    private static void CombineModeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // Changing the combine mode must re-run the boolean op, not reuse the stale mesh.
        if (a is CombinedGeometry combined) combined.InvalidateGeometry();
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
            rect = rect.TransformToAABB(matrix);   // was: result discarded (Rect is a value type)
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

        // Fast path for non-crossing input (clean nesting / disjoint shapes - e.g. a Border's outer/inner rounded
        // rect). The boolean result is then fixed by containment + mode alone, so we keep the BOUNDARY rings (the
        // ones across which mode-membership toggles) and earcut them via the even-odd nesting path - skipping the
        // O(n^2) intersection scan + segment marking + scanline that dominate an animated resize. Crossing input
        // (combined icon paths etc.) returns false and falls through to the full pipeline below.
        if (TryFastCombine(geometryType)) return;

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
                        // Intersect and Exclude both keep inner segments (RemoveSegmentsByRule(false)), so an
                        // arguable border segment resolved as outer must be dropped. Intersect was missing here,
                        // leaving spurious border edges in the result.
                        case GeometryCombineMode.Exclude:
                        case GeometryCombineMode.Intersect:
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

            var allSegmentsDict = allSegments.Distinct().ToDictionary(x=>x);

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
            // Bounding boxes don't overlap -> the shapes are disjoint, so the result depends on the mode:
            //   Union     -> both shapes
            //   Exclude   -> Geometry1 only (subtracting a disjoint Geometry2 changes nothing)
            //   Intersect -> empty (no overlap)
            // (Xor is handled above.) The previous code added both for every mode, which was wrong for
            // Intersect (should be empty) and Exclude (should be Geometry1 only).
            if (GeometryCombineMode is GeometryCombineMode.Union or GeometryCombineMode.Exclude)
            {
                foreach (var contour1 in OutlineMesh1.Contours)
                    Mesh.AddContour(contour1);
            }

            if (GeometryCombineMode == GeometryCombineMode.Union)
            {
                foreach (var contour2 in OutlineMesh2.Contours)
                    Mesh.AddContour(contour2);
            }

            // Triangulate only if geometry type is not Outlined
            if (geometryType == GeometryType.Outlined) return;

            if (Mesh.Contours.Count == 0)
            {
                Mesh.SetPoints(new List<Vector3>());
                return;
            }

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

    // We cannot provide one point join case as single contour for stroke generating, so we split for two separate contours
    // with one of the corners connected only visually, not logically
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
            } while (!Equals(currentSegment, startSegment) && currentSegment != null);

            strokeContours.Add(strokeContour);
        }

        return strokeContours;
    }

    // Combine two operands whose outlines do NOT cross (clean nesting / disjoint). Returns false (caller uses the full
    // boolean pipeline) for open operands, missing meshes, or any actual crossing/touch between rings.
    private bool TryFastCombine(GeometryType geometryType)
    {
        var rings = new List<(Vector2[] pts, int src)>();
        if (!CollectRings(Geometry1, 1, rings)) return false;
        if (!CollectRings(Geometry2, 2, rings)) return false;
        if (rings.Count == 0) return false;

        if (AnyRingsCross(rings)) return false;   // crossing/touching -> full pipeline

        // Keep a ring iff mode-membership differs just inside it vs just outside it (it bounds the result). Containment
        // parity per geometry comes from how many of the OTHER rings contain a vertex of this ring (non-crossing makes
        // that unambiguous); "just inside" additionally counts the ring itself.
        var kept = new List<Vector2[]>();
        for (var i = 0; i < rings.Count; i++)
        {
            int out1 = 0, out2 = 0;
            for (var j = 0; j < rings.Count; j++)
            {
                if (j == i || !PointInPolygon(rings[i].pts[0], rings[j].pts)) continue;
                if (rings[j].src == 1) out1++; else out2++;
            }
            var in1 = out1 + (rings[i].src == 1 ? 1 : 0);
            var in2 = out2 + (rings[i].src == 2 ? 1 : 0);

            if (ModeFill((in1 & 1) == 1, (in2 & 1) == 1) != ModeFill((out1 & 1) == 1, (out2 & 1) == 1))
                kept.Add(rings[i].pts);
        }

        foreach (var ring in kept) Mesh.AddContour(ring, true);

        if (geometryType == GeometryType.Outlined) return true;

        if (kept.Count == 0)
        {
            Mesh.SetPoints(new List<Vector3>());
            return true;
        }

        // The boundary rings reproduce the result region under even-odd; the nesting fast path earcuts it with holes.
        var polygon = new Polygon(FillRule.EvenOdd);
        foreach (var ring in kept) polygon.AddContour(new MeshContour(ring));
        Mesh.SetPoints(polygon.FillIndirect());
        return true;
    }

    private bool ModeFill(bool inG1, bool inG2) => GeometryCombineMode switch
    {
        GeometryCombineMode.Union => inG1 || inG2,
        GeometryCombineMode.Intersect => inG1 && inG2,
        GeometryCombineMode.Exclude => inG1 && !inG2,
        _ => inG1 ^ inG2,
    };

    // Closed, simple rings (>= 3 points) of an operand. False for a null/open operand or a degenerate contour, so the
    // caller drops to the full pipeline. A null operand contributes nothing (returns true with no rings added).
    private static bool CollectRings(Geometry g, int src, List<(Vector2[] pts, int src)> rings)
    {
        if (g == null) return true;
        if (g is not { IsClosed: true } || g.Mesh == null) return false;
        foreach (var c in g.Mesh.Contours)
        {
            if (!c.IsGeometryClosed || c.Points is not { Length: >= 3 }) return false;
            rings.Add((c.Points, src));
        }
        return true;
    }

    // True if any two non-adjacent edges (within or across rings) cross or touch. X-sweep broad-phase (~O(n log n) for
    // clean input). A shared vertex between adjacent edges of the same ring is fine; any other contact -> not clean.
    private static bool AnyRingsCross(List<(Vector2[] pts, int src)> rings)
    {
        var edges = new List<(Vector2 a, Vector2 b)>();
        foreach (var (pts, _) in rings)
            for (var i = 0; i < pts.Length; i++)
                edges.Add((pts[i], pts[(i + 1) % pts.Length]));
        edges.Sort((e, f) => Math.Min(e.a.X, e.b.X).CompareTo(Math.Min(f.a.X, f.b.X)));

        var active = new List<(Vector2 a, Vector2 b)>();
        foreach (var e in edges)
        {
            var minX = Math.Min(e.a.X, e.b.X);
            for (var i = active.Count - 1; i >= 0; i--)
                if (Math.Max(active[i].a.X, active[i].b.X) < minX) active.RemoveAt(i);

            foreach (var o in active)
            {
                if (e.a == o.a || e.a == o.b || e.b == o.a || e.b == o.b) continue; // shared vertex (adjacent) - fine
                if (SegmentsIntersect(e.a, e.b, o.a, o.b)) return true;
            }
            active.Add(e);
        }
        return false;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        int o1 = Orient(p1, p2, p3), o2 = Orient(p1, p2, p4), o3 = Orient(p3, p4, p1), o4 = Orient(p3, p4, p2);
        if (o1 != o2 && o3 != o4) return true;
        if (o1 == 0 && OnSegment(p1, p2, p3)) return true;
        if (o2 == 0 && OnSegment(p1, p2, p4)) return true;
        if (o3 == 0 && OnSegment(p3, p4, p1)) return true;
        if (o4 == 0 && OnSegment(p3, p4, p2)) return true;
        return false;
    }

    private static int Orient(Vector2 a, Vector2 b, Vector2 c)
    {
        var v = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        return v > 1e-9 ? 1 : (v < -1e-9 ? -1 : 0);
    }

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
        Math.Min(a.X, b.X) - 1e-9 <= p.X && p.X <= Math.Max(a.X, b.X) + 1e-9 &&
        Math.Min(a.Y, b.Y) - 1e-9 <= p.Y && p.Y <= Math.Max(a.Y, b.Y) + 1e-9;

    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
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