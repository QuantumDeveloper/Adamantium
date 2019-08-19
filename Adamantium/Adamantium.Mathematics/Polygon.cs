using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Adamantium.Mathematics
{
    /// <summary>
    /// Describes a collection of <see cref="PolygonItem"/>s, which will form a single merged triangulated polygon
    /// </summary>
    public class Polygon
    {
        /// <summary>
        /// Precision level for polygon triangulation
        /// </summary>
        /// <remarks>
        /// Seems Epsilon must be less than <see cref="MathHelper.ZeroToleranceD"/> on 1 magnitude to avoid floating point errors, but not less than 1e-8. 
        /// Current ZeroTolerance value = 1e-9
        /// </remarks>
        public static double Epsilon = 1e-8;
        /// <summary>
        /// Polygon name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Collection of <see cref="PolygonItem"/>
        /// </summary>
        public List<PolygonItem> Polygons { get; private set; }

        /// <summary>
        /// Collection of <see cref="PolygonItem"/>
        /// </summary>
        internal List<PolygonItem> PolygonsCopy { get; private set; }

        /// <summary>
        /// Colllection of points from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<Vector3D> MergedPoints { get; private set; }

        /// <summary>
        /// Colllection of segments from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<LineSegment> MergedSegments { get; private set; }

        /// <summary>
        /// Colllection of self intersected points from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<Vector3D> SelfIntersectedPoints;

        /// <summary>
        /// Defines presence of self intersection
        /// </summary>
        public bool HasSelfIntersections => SelfIntersectedPoints.Count > 0;

        /// <summary>
        /// Collection of self intersected segments
        /// </summary>
        public List<LineSegment> SelfIntersectedSegments;

        /// <summary>
        /// Highest point in polygon 
        /// </summary>
        public Vector3D HighestPoint;

        /// <summary>
        /// Triangulation rule (true - Even-Odd, false - Non-Zero)
        /// </summary>
        public FillRule FillRule { get; set; }

        private readonly List<PolygonPair> checkedPolygons;
        private object vertexLocker = new object();

        /// <summary>
        /// Constructs <see cref="Polygon"/>
        /// </summary>
        public Polygon()
        {
            Polygons = new List<PolygonItem>();
            MergedPoints = new List<Vector3D>();
            MergedSegments = new List<LineSegment>();
            SelfIntersectedPoints = new List<Vector3D>();
            SelfIntersectedSegments = new List<LineSegment>();
            checkedPolygons = new List<PolygonPair>();
            FillRule = FillRule.EvenOdd;
        }


        /// <summary>
        /// Triangulate current <see cref="Polygon"/>
        /// </summary>
        /// <returns></returns>
        public List<Vector3F> Fill()
        {
            PolygonsCopy = new List<PolygonItem>(Polygons);

            Parallel.For(0, PolygonsCopy.Count, i =>
            {
                PolygonsCopy[i].SplitOnSegments();
                PolygonsCopy[i].CheckForSelfIntersection(FillRule);
            });

            var polygons = CheckPolygonItemIntersections();

            var vertices = new List<Vector3F>();

            Parallel.ForEach(polygons, item =>
            {
                var result1 = TriangulatePolygonItem(item);
                vertices.AddRange(result1);
            });

            MergePoints();
            MergeSegments();

            MergeSelfIntersectedPoints();
            MergeSelfIntersectedSegments();

            CheckPolygonsIntersection();

            UpdateBoundingBox();

            var result = Triangulator.Triangulate(this);

            vertices.AddRange(result);

            return vertices;
        }

        private List<Vector3F> TriangulatePolygonItem(PolygonItem item)
        {
            var itemCopy = item;
            var polygon = new Polygon();
            polygon.MergedPoints = itemCopy.Points;
            polygon.MergedSegments = itemCopy.Segments;
            polygon.SelfIntersectedPoints = itemCopy.SelfIntersectedPoints;
            polygon.SelfIntersectedSegments = itemCopy.SelfIntersectedSegments;

            polygon.UpdateBoundingBox();

            return Triangulator.Triangulate(this);
        }

        private List<PolygonItem> CheckPolygonItemIntersections()
        {
            var polygonsList = new List<PolygonItem>();

            if (PolygonsCopy.Count <= 1)
            {
                return polygonsList;
            }

            foreach(var polygon1 in PolygonsCopy)
            {
                foreach(var polygon2 in PolygonsCopy)
                {
                    if (polygon1 == polygon2) continue;

                    var bb2 = polygon2.BoundingBox;
                    var containment = polygon1.BoundingBox.Contains(ref bb2);
                    if (containment != ContainmentType.Disjoint)
                    {
                        break;
                    }

                    polygonsList.Add(polygon1);
                }
            }

            foreach(var p in polygonsList)
            {
                if (PolygonsCopy.Contains(p))
                {
                    PolygonsCopy.Remove(p);
                }
            }

            return polygonsList;
        }

        /// <summary>
        /// Merge points from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergePoints()
        {
            MergedPoints.Clear();
            for (int i = 0; i < PolygonsCopy.Count; ++i)
            {
                MergedPoints.AddRange(PolygonsCopy[i].Points);
            }
        }

        /// <summary>
        /// Merge selfIntersected points from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSelfIntersectedPoints()
        {
            SelfIntersectedPoints.Clear();
            for (int i = 0; i < PolygonsCopy.Count; ++i)
            {
                SelfIntersectedPoints.AddRange(PolygonsCopy[i].SelfIntersectedPoints);
            }
        }

        /// <summary>
        /// Merge segments from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSegments()
        {
            MergedSegments.Clear();
            for (int i = 0; i < PolygonsCopy.Count; ++i)
            {
                MergedSegments.AddRange(PolygonsCopy[i].Segments);
            }
        }

        /// <summary>
        /// Merge selfIntersected segments from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSelfIntersectedSegments()
        {
            SelfIntersectedSegments.Clear();
            for (int i = 0; i < PolygonsCopy.Count; ++i)
            {
                SelfIntersectedSegments.AddRange(PolygonsCopy[i].SelfIntersectedSegments);
            }
        }

        /// <summary>
        /// Checking intersection between all <see cref="PolygonItem"/>s in <see cref="Polygon"/>
        /// </summary>
        private void CheckPolygonsIntersection()
        {
            if (PolygonsCopy.Count <= 1)
            {
                return;
            }

            List<Vector3D> edgePoints = new List<Vector3D>();
            for (int i = 0; i < PolygonsCopy.Count; i++)
            {
                var polygon1 = PolygonsCopy[i];
                for (int j = 0; j < PolygonsCopy.Count; j++)
                {
                    var polygon2 = PolygonsCopy[j];
                    if (polygon1 == polygon2 || ContainsPolygonPair(polygon1, polygon2))
                    {
                        continue;
                    }

                    //Check if polygons are inside each other and if yes, then mark inner as a Hole
                    var intersectionResult = polygon1.IsCompletelyContains(polygon2);
                    if (intersectionResult == ContainmentType.Contains)
                    {
                        polygon2.IsHole = true;
                    }
                    else if (polygon2.IsCompletelyContains(polygon1) == ContainmentType.Contains)
                    {
                        polygon1.IsHole = true;
                    }

                    AddPolygonsPairsToList(polygon1, polygon2, intersectionResult);
                }
            }

            foreach (var polygonPair in checkedPolygons)
            {
                var polygon1 = polygonPair.Polygon1;
                var polygon2 = polygonPair.Polygon2;
                var intersectionResult = polygonPair.IntersectionType;

                //If polygons partly inside each other
                if (intersectionResult == ContainmentType.Intersects)
                {
                    FindEdgeIntersectionPoints(polygon1, polygon2, out edgePoints);
                }

                for (int k = 0; k < edgePoints.Count; ++k)
                {
                    if (!SelfIntersectedPoints.Contains(edgePoints[k]))
                    {
                        SelfIntersectedPoints.Add(edgePoints[k]);
                    }
                }

                //Split existing segments on more segmnets on edge points
                UpdatePolygonUsingSelfPoints(edgePoints);

                //If Non-zero and smallest polygon is not a hole, lets find a list of points describing all possible connections between smallest and biggest polygons
                //and remove all segments, which connects any of two intersection points
                if (FillRule == FillRule.NonZero && intersectionResult == ContainmentType.Intersects && !polygon1.IsHole && !polygon2.IsHole)
                {
                    var lst = FindAllPossibleIntersections(polygon1, polygon2, edgePoints);
                    RemoveInternalSegments(lst);
                }
            }
        }

        internal void RemoveInternalSegments(List<Vector3D> interPoints)
        {
            //Remove all segments based on intersection points in case of Non-Zero rule
            if (FillRule == FillRule.NonZero && interPoints.Count > 0)
            {
                for (int i = 0; i < interPoints.Count; i++)
                {
                    for (int j = 0; j < interPoints.Count; j++)
                    {
                        var point1 = interPoints[i];
                        var point2 = interPoints[j];
                        if (MathHelper.WithinEpsilon(point1, point2, Epsilon))
                        { continue; }

                        LineSegment segment;
                        if (IsConnected(interPoints[i], interPoints[j], MergedSegments, out segment))
                        {
                            if (!IsBelongToPolygonWithHole(ref segment))
                            {
                                MergedSegments.Remove(segment);
                            }
                        }
                    }
                }
                UpdateMergedPoints();
            }
        }

        /// <summary>
        /// This method checks if certain segment belongs to polygon which is defined as hole.
        /// If so, its segment should not be deleted, because it shared between several polygons 
        /// and could produce incorrect results in cases when one of the polygons intersects 
        /// with 2 or more polygons for NonZero triangulation rule
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        private bool IsBelongToPolygonWithHole(ref LineSegment segment)
        {
            foreach (var polygon in PolygonsCopy)
            {
                if (!polygon.IsHole)
                {
                    continue;
                }

                foreach (var polygonSegment in polygon.Segments)
                {
                    if (polygonSegment == segment)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool FindEdgeIntersectionPoints(PolygonItem polygon1, PolygonItem polygon2, out List<Vector3D> edgePoints)
        {
            edgePoints = new List<Vector3D>();
            for (int i = 0; i < polygon1.Segments.Count; i++)
            {
                for (int j = 0; j < polygon2.Segments.Count; j++)
                {
                    var segment1 = polygon1.Segments[i];
                    var segment2 = polygon2.Segments[j];
                    Vector3D point;
                    if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out point))
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


        /// <summary>
        /// Find all possible intersections between 2 polygons including intersection points and remove all merged segments 
        /// if any of 2 points are connected as segments. This needs for Non-zero rule to remove all inner segments between 2 polygons to correctly triangulate them
        /// </summary>
        /// <param name="polygon1">first polygon</param>
        /// <param name="polygon2">second polygon</param>
        /// <param name="edgePoints">intersection points between 2 polygons, which were found earlier on previous step</param>
        /// <returns>Collection of intersection points, including points, which is not lying on segments, but also inside polygons.</returns>
        /// <remarks>During running this method, it will remove all merged segments from <see cref="Polygon"/> if some of its segments contains both intersection points</remarks>
        private List<Vector3D> FindAllPossibleIntersections(PolygonItem polygon1, PolygonItem polygon2, List<Vector3D> edgePoints)
        {
            var allIntersections = new List<Vector3D>(edgePoints);

            for (int i = 0; i < polygon1.Points.Count; i++)
            {
                if (IsPointInsideArea(polygon1.Points[i], polygon2.Segments) &&
                    !allIntersections.Contains(polygon1.Points[i]))
                {
                    allIntersections.Add(polygon1.Points[i]);
                }
            }
            for (int i = 0; i < polygon2.Points.Count; i++)
            {
                var point = polygon2.Points[i];
                if (IsPointInsideArea(point, polygon1.Segments) &&
                    !allIntersections.Contains(point))
                {
                    allIntersections.Add(point);
                }
            }

            return allIntersections;
        }


        /// <summary>
        /// Adds polygons to checklist to not process this polygon pair again
        /// </summary>
        /// <param name="polygon1">First polygon</param>
        /// <param name="polygon2">Second polygon</param>
        /// <param name="intersectionType">Intersection type between two polygons</param>
        private void AddPolygonsPairsToList(PolygonItem polygon1, PolygonItem polygon2, ContainmentType intersectionType)
        {
            checkedPolygons.Add(new PolygonPair(polygon1, polygon2, intersectionType));
        }

        /// <summary>
        /// Check does 2 polygons present in check list
        /// </summary>
        /// <param name="polygon1">First polygon</param>
        /// <param name="polygon2">Second polygon</param>
        /// <returns>True if polygons already contains in check list, otherwise false</returns>
        private bool ContainsPolygonPair(PolygonItem polygon1, PolygonItem polygon2)
        {
            return checkedPolygons.Contains(new PolygonPair(polygon1, polygon2));
        }

        /// <summary>
        /// Sort points by its X component from smallest to largest
        /// </summary>
        /// <returns>New collection of </returns>
        public List<Vector3D> SortVertices()
        {
            var sortedList = new List<Vector3D>(MergedPoints);
            sortedList.Sort(VertexComparer.Default);
            return sortedList;
        }

        private void UpdateBoundingBox()
        {
            if (MergedPoints.Count > 0)
            {
                Vector3D minimum = new Vector3D(double.MaxValue);
                Vector3D maximum = new Vector3D(double.MinValue);

                for (int i = 0; i < MergedPoints.Count; ++i)
                {
                    var point = MergedPoints[i];
                    Vector3D.Min(ref minimum, ref point, out minimum);
                    Vector3D.Max(ref maximum, ref point, out maximum);
                }
                HighestPoint = new Vector3D(minimum.X, maximum.Y, 0);
            }
        }

        /// <summary>
        /// Checks if point is completely inside polygon, formed by segments
        /// </summary>
        /// <param name="point"></param>
        /// <param name="area"></param>
        /// <returns>True if point is completely inside polygon, otherwise false</returns>
        internal static bool IsPointInsideArea(Vector3D point, List<LineSegment> area)
        {
            var rayLeft = new Ray2D((Vector2D)point, -Vector2D.UnitX);
            var rayDown = new Ray2D((Vector2D)point, -Vector2D.UnitY);
            var rayRight = new Ray2D((Vector2D)point, Vector2D.UnitX);
            var rayUp = new Ray2D((Vector2D)point, Vector2D.UnitY);
            IntersectionSides sides = IntersectionSides.None;
            for (int i = 0; i < area.Count; i++)
            {
                Vector3D interPoint;
                var segment = area[i];
                if (Collision2D.RaySegmentIntersection(ref rayLeft, ref segment, out interPoint))
                {
                    sides |= IntersectionSides.Left;
                }

                if (Collision2D.RaySegmentIntersection(ref rayDown, ref segment, out interPoint))
                {
                    sides |= IntersectionSides.Down;
                }

                if (Collision2D.RaySegmentIntersection(ref rayRight, ref segment, out interPoint))
                {
                    sides |= IntersectionSides.Right;
                }

                if (Collision2D.RaySegmentIntersection(ref rayUp, ref segment, out interPoint))
                {
                    sides |= IntersectionSides.Up;
                }

                if (sides.HasFlag(IntersectionSides.All))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Updates segments for polygons using selfintersection points
        /// </summary>
        /// <param name="selfIntersectionPoints">Selfintersection points</param>
        private void UpdatePolygonUsingSelfPoints(List<Vector3D> selfIntersectionPoints)
        {
            if (selfIntersectionPoints.Count == 0)
            {
                return;
            }

            List<LineSegment> tempSegments = new List<LineSegment>(MergedSegments);
            for (int i = 0; i < selfIntersectionPoints.Count; i++)
            {
                var interPoint = selfIntersectionPoints[i];
                List<LineSegment> segments;
                if (GetSegmentsFromPoint(tempSegments, interPoint, out segments))
                {
                    for (int j = 0; j < segments.Count; j++)
                    {
                        var segment = segments[j];
                        //if (interPoint == segment.Start || interPoint == segment.End)
                        if (MathHelper.WithinEpsilon(interPoint, segment.Start, Epsilon) ||
                            MathHelper.WithinEpsilon(interPoint, segment.End, Epsilon))
                        {
                            continue;
                        }

                        var index = tempSegments.IndexOf(segment);
                        tempSegments.RemoveAt(index);
                        LineSegment seg1 = new LineSegment(segment.Start, interPoint);
                        //if (ContainsInInterPointsKeepHoles(seg1, selfIntersectionPoints, FillRule, PolygonsCopy))
                        {
                            if (InsertSegment(tempSegments, seg1, index))
                            {
                                index++;
                            }
                        }

                        LineSegment seg2 = new LineSegment(interPoint, segment.End);
                        //if (ContainsInInterPointsKeepHoles(seg2, selfIntersectionPoints, FillRule, PolygonsCopy))
                        {
                            if (InsertSegment(tempSegments, seg2, index))
                            {
                            }
                        }
                    }
                }
            }
            MergedSegments.Clear();
            MergedSegments.AddRange(tempSegments);

            UpdateMergedPoints();

        }

        /// <summary>
        /// Update <see cref="MergedSegments"/> collection based on collection of intersection points. 
        /// As a result, this method will create new additional segments nad update <see cref="MergedPoints"/> collection
        /// </summary>
        /// <param name="intersectionPoints">Collection of intersection points</param>
        internal void UpdatePolygonUsingRayInterPoints(List<Vector3D> intersectionPoints)
        {
            if (intersectionPoints.Count == 0)
            {
                return;
            }

            List<LineSegment> tempSegments = new List<LineSegment>(MergedSegments);
            for (int i = 0; i < intersectionPoints.Count; i++)
            {
                var interPoint = intersectionPoints[i];
                List<LineSegment> segments;
                if (GetSegmentsFromPoint(tempSegments, interPoint, out segments))
                {
                    for (int j = 0; j < segments.Count; j++)
                    {
                        var segment = segments[j];
                        if (MathHelper.WithinEpsilon(interPoint, segment.Start, Epsilon) ||
                            MathHelper.WithinEpsilon(interPoint, segment.End, Epsilon))
                        {
                            continue;
                        }

                        var index = tempSegments.IndexOf(segment);
                        tempSegments.RemoveAt(index);
                        LineSegment seg1 = new LineSegment(segment.Start, interPoint);
                        if (InsertSegment(tempSegments, seg1, index))
                        {
                            index++;
                        }

                        LineSegment seg2 = new LineSegment(interPoint, segment.End);
                        InsertSegment(tempSegments, seg2, index);
                    }
                }
            }
            MergedSegments.Clear();
            MergedSegments.AddRange(tempSegments);

            UpdateMergedPoints();
        }

        /// <summary>
        /// Updates collection of <see cref="MergedPoints"/> based on <see cref="MergedSegments"/> to have correct order of point for <see cref="Polygon"/>
        /// </summary>
        private void UpdateMergedPoints()
        {
            MergedPoints.Clear();
            for (int i = 0; i < MergedSegments.Count; ++i)
            {
                if (!MergedPoints.Contains(MergedSegments[i].Start))
                {
                    MergedPoints.Add(MergedSegments[i].Start);
                }

                if (!MergedPoints.Contains(MergedSegments[i].End))
                {
                    MergedPoints.Add(MergedSegments[i].End);
                }
            }
        }

        /// <summary>
        /// Checks does all points in array belongs to polygon, which forms a hole
        /// </summary>
        /// <param name="polygons">Collection of <see cref="PolygonItem"/></param>
        /// <param name="points">Array of points to check</param>
        /// <returns>True if all points are belongs to hole polygone, otherwise false</returns>
        internal static bool PointsInHole(List<PolygonItem> polygons, params Vector3D[] points)
        {
            for (int i = 0; i < polygons.Count; ++i)
            {
                if (polygons[i].IsHole)
                {
                    var polygon = polygons[i];
                    int cnt = 0;
                    for (int k = 0; k < points.Length; k++)
                    {
                        for (int j = 0; j < polygon.Segments.Count; j++)
                        {
                            var segment = polygon.Segments[j];
                            if (Collision2D.IsPointOnSegment(ref segment, ref points[k]))
                            {
                                cnt++;
                                break;
                            }
                        }
                    }
                    if (cnt == points.Length)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a collection of segments from a collection of points
        /// </summary>
        /// <param name="points"></param>
        /// <returns>Collection of segments if at least 2 points present in collection, otherwise null</returns>
        public static List<LineSegment> SplitOnSegments(List<Vector3D> points)
        {
            if (points.Count > 1)
            {
                List<LineSegment> segments = new List<LineSegment>();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var segment = new LineSegment(points[i], points[i + 1]);
                    if (!IsSameStartEnd(ref segment))
                    {
                        segments.Add(segment);
                    }
                }
                var seg = new LineSegment(points[points.Count - 1], points[0]);
                if (!segments.Contains(seg))
                {
                    //Add last segment
                    if (!IsSameStartEnd(ref seg))
                    {
                        segments.Add(seg);
                    }
                }
                return segments;
            }
            return null;
        }

        /// <summary>
        /// Inserts segment in collection of segments at specifies index
        /// </summary>
        /// <param name="segments">Segments collection</param>
        /// <param name="segment">Segment to insert</param>
        /// <param name="index">Index to insert at</param>
        /// <returns>True if segment collection does not already has such segment, otherwise false</returns>
        public static bool InsertSegment(List<LineSegment> segments, LineSegment segment, int index)
        {
            if (!segments.Contains(segment) && !IsSameStartEnd(ref segment))
            {
                segments.Insert(index, segment);
                return true;
            }
            return false;
        }

        internal static bool IsSameStartEnd(ref LineSegment segment)
        {
            if (segment.Start == segment.End)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if two points forms single segment in collection of segments
        /// </summary>
        /// <param name="start">Start segment point</param>
        /// <param name="end">End segment point</param>
        /// <param name="segments">Collection of segments</param>
        /// <returns>True is points connected, otherwise false</returns>
        public static bool IsConnected(Vector3D start, Vector3D end, List<LineSegment> segments)
        {
            LineSegment segment;
            return IsConnected(start, end, segments, out segment);
        }

        /// <summary>
        /// Checks if two points forms single segment in collection of segments and output correctly linked segment containing two points
        /// </summary>
        /// <param name="start">Start segment point</param>
        /// <param name="end">End segment point</param>
        /// <param name="segments">Collection of segments</param>
        /// <param name="correctSegment">Correct segment from start and end points</param>
        /// <returns>True is points connected, otherwise false</returns>
        /// <remarks>
        /// Segment could be connected not the same way as parameters. For ex. it could starts from end point and end in start point. 
        /// This method will also check for such cases and will check for segments very close to each other (less then <see cref="Epsilon"/>) 
        /// to 100% find correct segment
        /// </remarks>
        public static bool IsConnected(Vector3D start, Vector3D end, List<LineSegment> segments, out LineSegment correctSegment)
        {
            LineSegment segment = new LineSegment(start, end);
            correctSegment = segment;
            for (int i = 0; i < segments.Count; ++i)
            {
                var currentSegment = segments[i];
                if (Collision2D.IsPointOnSegment(ref currentSegment, ref start)
                    && Collision2D.IsPointOnSegment(ref currentSegment, ref end))
                {
                    correctSegment = currentSegment;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Find all segments that lies on certain point in segments collection
        /// </summary>
        /// <param name="segments">Collection of segments</param>
        /// <param name="point">point in 2D</param>
        /// <param name="foundSegments">Collection of found segments</param>
        /// <returns>True if at least one segment was found, otherwise false</returns>
        public static bool GetSegmentsFromPoint(List<LineSegment> segments, Vector3D point, out List<LineSegment> foundSegments)
        {
            foundSegments = new List<LineSegment>();
            for (int i = 0; i < segments.Count; ++i)
            {
                var s = segments[i];
                if (Collision2D.IsPointOnSegment(ref s, ref point))
                {
                    foundSegments.Add(segments[i]);
                }
            }
            return foundSegments.Count > 0;
        }

        /// <summary>
        /// Check does collection of points forms a concave <see cref="PolygonItem"/>
        /// </summary>
        /// <param name="points">Collection of points to check</param>
        /// <returns>True if polygon is concave, otherwise false</returns>
        public static bool IsConcave(List<Vector3D> points)
        {
            for (int i = 0; i < points.Count - 2; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var p2 = points[i + 2];

                var vec0 = p0 - p1;
                var vec1 = p2 - p1;
                vec0.Normalize();
                vec1.Normalize();

                var angle = MathHelper.AngleBetween(vec0, vec1);
                if (angle > 180)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check does collection of segments forms a concave <see cref="PolygonItem"/>
        /// </summary>
        /// <param name="segments">Collection of segments to check</param>
        /// <returns>True if polygon is concave, otherwise false</returns>
        public static bool IsConcave(List<LineSegment> segments)
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var vec0 = segments[i].DirectionNormalized;
                var vec1 = segments[i + 1].DirectionNormalized;

                var angle = MathHelper.AngleBetween(vec0, vec1);
                if (angle > 180)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Comparer here is for sorting vectors by its X component from smallest to biggest value
        /// </summary>
        private class VertexComparer : IComparer<Vector3D>
        {
            public static VertexComparer Default => new VertexComparer();

            public int Compare(Vector3D x, Vector3D y)
            {
                if (MathHelper.WithinEpsilon(x.X, y.X, Epsilon))
                {
                    return 0;
                }
                if (x.X < y.X)
                {
                    return -1;
                }
                return 1;
            }
        }

        public override string ToString()
        {
            return $"{Name}, PolygonItems: {PolygonsCopy.Count}";
        }

        private class PolygonPair: IEquatable<PolygonPair>
        {
            public PolygonItem Polygon1 { get; set; }

            public PolygonItem Polygon2 { get; set; }

            public ContainmentType IntersectionType { get; set; }

            public PolygonPair() {}

            public PolygonPair(PolygonItem polygon1, PolygonItem polygon2, ContainmentType intersectionType)
            {
                Polygon1 = polygon1;
                Polygon2 = polygon2;
                IntersectionType = intersectionType;
            }

            public PolygonPair(PolygonItem polygon1, PolygonItem polygon2)
            {
                Polygon1 = polygon1;
                Polygon2 = polygon2;
            }

            public bool Equals(PolygonPair other)
            {
                if (ReferenceEquals(null, other))
                    return false;
                if (ReferenceEquals(this, other))
                    return true;
                return (Equals(Polygon1, other.Polygon1) && Equals(Polygon2, other.Polygon2)) ||
                       (Equals(Polygon1, other.Polygon2) && Equals(Polygon2, other.Polygon1));
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                if (ReferenceEquals(this, obj))
                    return true;
                if (obj.GetType() != this.GetType())
                    return false;
                return Equals((PolygonPair) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Polygon1 != null ? Polygon1.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Polygon2 != null ? Polygon2.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (int) IntersectionType;
                    return hashCode;
                }
            }

            public override string ToString()
            {
                return $"Polygon1 {Polygon1}, Polygon2 {Polygon2}, IntersectionType {IntersectionType}";
            }
        }
    }
}
