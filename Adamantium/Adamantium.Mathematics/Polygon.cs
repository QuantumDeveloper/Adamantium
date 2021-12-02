using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Adamantium.Mathematics
{
    /// <summary>
    /// Describes a collection of <see cref="PolygonItem"/>s, which will form a single merged triangulated polygon
    /// </summary>
    public class Polygon
    {
        private readonly HashSet<Vector2D> mergedPointsHash;
        private readonly List<PolygonItem> polygons;

        /// <summary>
        /// Precision level for polygon triangulation
        /// </summary>
        /// <remarks>
        /// Seems Epsilon must be less than <see cref="MathHelper.ZeroToleranceD"/> on 1 magnitude to avoid floating point errors, but not less than 1e-8. 
        /// Current ZeroTolerance value = 1e-9
        /// </remarks>
        public static double Epsilon = MathHelper.ZeroToleranceD;
        /// <summary>
        /// Polygon name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Collection of <see cref="PolygonItem"/>
        /// </summary>
        public IReadOnlyCollection<PolygonItem> Polygons => polygons.AsReadOnly();

        /// <summary>
        /// Collection of points from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<Vector2D> MergedPoints { get; private set; }

        /// <summary>
        /// Collection of segments from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<LineSegment2D> MergedSegments { get; private init; }

        /// <summary>
        /// Collection of self intersected points from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<Vector2D> SelfIntersectedPoints { get; private init; }

        /// <summary>
        /// Defines presence of self intersection
        /// </summary>
        public bool HasSelfIntersections => SelfIntersectedPoints.Count > 0;

        /// <summary>
        /// Collection of self intersected segments
        /// </summary>
        public List<LineSegment2D> SelfIntersectedSegments { get; private init; }

        /// <summary>
        /// Highest point in polygon 
        /// </summary>
        public Vector2D HighestPoint;

        /// <summary>
        /// Triangulation rule (true - Even-Odd, false - Non-Zero)
        /// </summary>
        public FillRule FillRule { get; set; }

        private readonly List<PolygonPair> checkedPolygons;
        private readonly object vertexLocker = new object();

        /// <summary>
        /// Constructs <see cref="Polygon"/>
        /// </summary>
        public Polygon()
        {
            polygons = new List<PolygonItem>();
            MergedPoints = new List<Vector2D>();
            mergedPointsHash = new HashSet<Vector2D>();
            MergedSegments = new List<LineSegment2D>();
            SelfIntersectedPoints = new List<Vector2D>();
            SelfIntersectedSegments = new List<LineSegment2D>();
            checkedPolygons = new List<PolygonPair>();
            FillRule = FillRule.EvenOdd;
        }

        public void AddItem(PolygonItem item)
        {
            polygons.Add(item);
        }

        public void AddItems(params PolygonItem[] items)
        {
            polygons.AddRange(items);
        }
        
        /// <summary>
        /// Triangulate current <see cref="Polygon"/>
        /// </summary>
        /// <returns></returns>
        public List<Vector3F> Fill()
        {
            Parallel.For(0, polygons.Count, i =>
            {
                polygons[i].SplitOnSegments();
                polygons[i].CheckForSelfIntersection(FillRule);
            });

            var nonIntersectedPolygons = CheckPolygonItemIntersections();

            var vertices = new List<Vector3F>();

            if (nonIntersectedPolygons.Count > 1)
            {
                Parallel.ForEach(nonIntersectedPolygons, item =>
                {
                    var result1 = TriangulatePolygonItem(item);
                    lock (vertexLocker)
                    {
                        vertices.AddRange(result1);
                    }
                });
            }

            if (polygons.Count > 0)
            {
                MergePoints();
                MergeSegments();

                MergeSelfIntersectedPoints();
                MergeSelfIntersectedSegments();

                CheckPolygonsIntersection();

                UpdateBoundingBox();

                var result = Triangulator.Triangulate(this);
                lock (vertexLocker)
                {
                    vertices.AddRange(result);
                }
            }

            return vertices;
        }

        private List<Vector3F> TriangulatePolygonItem(PolygonItem item)
        {
            var itemCopy = item;
            var polygon = new Polygon
            {
                Name = Name,
                MergedPoints = itemCopy.Points,
                MergedSegments = itemCopy.Segments,
                SelfIntersectedPoints = itemCopy.SelfIntersectedPoints,
                SelfIntersectedSegments = itemCopy.SelfIntersectedSegments
            };

            polygon.UpdateBoundingBox();

            return Triangulator.Triangulate(polygon);
        }

        private List<PolygonItem> CheckPolygonItemIntersections()
        {
            var polygonsList = new List<PolygonItem>();

            if (Polygons.Count <= 1)
            {
                return polygonsList;
            }

            foreach(var polygon1 in Polygons)
            {
                var canAdd = true;
                foreach(var polygon2 in Polygons)
                {
                    if (polygon1 == polygon2) continue;

                    var bb1 = polygon1.BoundingBox;
                    var bb2 = polygon2.BoundingBox;
                    // var containment1 = polygon1.BoundingBox.Contains(bb2);
                    // var containment2 = polygon2.BoundingBox.Contains(bb1);
                    
                    var containment1 = polygon1.BoundingBox.Intersects(bb2);
                    var containment2 = polygon2.BoundingBox.Intersects(bb1);

                    if (containment1 || containment2)
                    {
                        canAdd = false;
                        break;
                    }
                }

                if (canAdd)
                {
                    polygonsList.Add(polygon1);
                }
            }

            foreach(var p in polygonsList)
            {
                if (polygons.Contains(p))
                {
                    polygons.Remove(p);
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
            for (var i = 0; i < polygons.Count; ++i)
            {
                MergedPoints.AddRange(polygons[i].Points);
            }
        }

        /// <summary>
        /// Merge selfIntersected points from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSelfIntersectedPoints()
        {
            SelfIntersectedPoints.Clear();
            for (var i = 0; i < polygons.Count; ++i)
            {
                SelfIntersectedPoints.AddRange(polygons[i].SelfIntersectedPoints);
            }
        }

        /// <summary>
        /// Merge segments from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSegments()
        {
            MergedSegments.Clear();
            for (var i = 0; i < polygons.Count; ++i)
            {
                MergedSegments.AddRange(polygons[i].Segments);
            }
        }

        /// <summary>
        /// Merge selfIntersected segments from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSelfIntersectedSegments()
        {
            SelfIntersectedSegments.Clear();
            for (var i = 0; i < Polygons.Count; ++i)
            {
                SelfIntersectedSegments.AddRange(polygons[i].SelfIntersectedSegments);
            }
        }

        /// <summary>
        /// Checking intersection between all <see cref="PolygonItem"/>s in <see cref="Polygon"/>
        /// </summary>
        private void CheckPolygonsIntersection()
        {
            if (polygons.Count <= 1)
            {
                return;
            }

            var edgePoints = new List<Vector2D>();
            for (var i = 0; i < polygons.Count; i++)
            {
                var polygon1 = polygons[i];
                for (var j = 0; j < Polygons.Count; j++)
                {
                    var polygon2 = polygons[j];
                    if (polygon1 == polygon2 || ContainsPolygonPair(polygon1, polygon2))
                    {
                        continue;
                    }

                    //Check if polygons are inside each other and if yes, then mark inner as a Hole
                    var intersectionResult = polygon1.IsCompletelyContains(polygon2);
                    
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

                for (var k = 0; k < edgePoints.Count; ++k)
                {
                    if (!SelfIntersectedPoints.Contains(edgePoints[k]))
                    {
                        SelfIntersectedPoints.Add(edgePoints[k]);
                    }
                }

                //Split existing segments on more segments on edge points
                UpdatePolygonUsingSelfPoints(edgePoints);

                // If Non-zero and smallest polygon is not a hole, lets find a list of points describing all possible
                // connections between smallest and biggest polygons and remove all segments,
                // which connects any of two intersection points
                if (FillRule == FillRule.NonZero && intersectionResult == ContainmentType.Intersects)
                {
                    var lst = FindAllPossibleIntersections(polygon1, polygon2, edgePoints);
                    RemoveInternalSegments(lst);
                }
            }
        }

        /// <summary>
        /// Remove all segments based on intersection points in case of Non-Zero rule
        /// </summary>
        /// <param name="interPoints"></param>
        private void RemoveInternalSegments(List<Vector2D> interPoints)
        {
            if (FillRule != FillRule.NonZero || interPoints.Count <= 0) return;
            for (var i = 0; i < interPoints.Count; i++)
            {
                for (var j = 0; j < interPoints.Count; j++)
                {
                    var point1 = interPoints[i];
                    var point2 = interPoints[j];
                    if (MathHelper.WithinEpsilon(point1, point2, Epsilon))
                    { continue; }

                    if (IsConnected(point1, point2, MergedSegments, out var segment2D))
                    {
                        MergedSegments.Remove(segment2D);
                    }
                }
            }
            UpdateMergedPoints();
        }

        private bool FindEdgeIntersectionPoints(PolygonItem polygon1, PolygonItem polygon2, out List<Vector2D> edgePoints)
        {
            edgePoints = new List<Vector2D>();
            for (var i = 0; i < polygon1.Segments.Count; i++)
            {
                for (var j = 0; j < polygon2.Segments.Count; j++)
                {
                    var segment1 = polygon1.Segments[i];
                    var segment2 = polygon2.Segments[j];
                    if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out var point))
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
        private List<Vector2D> FindAllPossibleIntersections(PolygonItem polygon1, PolygonItem polygon2, List<Vector2D> edgePoints)
        {
            var allIntersections = new List<Vector2D>(edgePoints);

            for (var i = 0; i < polygon1.Points.Count; i++)
            {
                var point = polygon1.Points[i];
                if (IsPointInsideArea(point, polygon2.Segments) &&
                    !allIntersections.Contains(point))
                {
                    allIntersections.Add(point);
                }
            }
            for (var i = 0; i < polygon2.Points.Count; i++)
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
        public List<Vector2D> SortVertices()
        {
            var sortedList = new List<Vector2D>(MergedPoints);
            sortedList.Sort(VerticalVertexComparer.Default);
            return sortedList;
        }

        private void UpdateBoundingBox()
        {
            if (MergedPoints.Count <= 0) return;
            var minimum = new Vector2D(double.MaxValue);
            var maximum = new Vector2D(double.MinValue);

            for (var i = 0; i < MergedPoints.Count; ++i)
            {
                var point = MergedPoints[i];
                Vector2D.Min(ref minimum, ref point, out minimum);
                Vector2D.Max(ref maximum, ref point, out maximum);
            }
            //HighestPoint = new Vector2D(minimum.X, maximum.Y);
            HighestPoint = new Vector2D(minimum.X, minimum.Y);
        }

        /// <summary>
        /// Checks if point is completely inside polygon, formed by segments
        /// </summary>
        /// <param name="point"></param>
        /// <param name="area"></param>
        /// <returns>True if point is completely inside polygon, otherwise false</returns>
        internal static bool IsPointInsideArea(Vector2D point, List<LineSegment2D> area)
        {
            var rayLeft = new Ray2D(point, -Vector2D.UnitX);
            var rayDown = new Ray2D(point, -Vector2D.UnitY);
            var rayRight = new Ray2D(point, Vector2D.UnitX);
            var rayUp = new Ray2D(point, Vector2D.UnitY);
            var sides = IntersectionSides.None;
            for (var i = 0; i < area.Count; i++)
            {
                var segment = area[i];
                if (Collision2D.RaySegmentIntersection(ref rayLeft, ref segment, out var interPoint))
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
        /// Updates segments for polygons using self Intersection points
        /// </summary>
        /// <param name="selfIntersectionPoints">Self intersection points</param>
        private void UpdatePolygonUsingSelfPoints(List<Vector2D> selfIntersectionPoints)
        {
            if (selfIntersectionPoints.Count == 0)
            {
                return;
            }

            var tempSegments = new List<LineSegment2D>(MergedSegments);
            for (var i = 0; i < selfIntersectionPoints.Count; i++)
            {
                var interPoint = selfIntersectionPoints[i];
                if (!GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
                for (var j = 0; j < segments.Count; j++)
                {
                    var segment = segments[j];
                    
                    if (MathHelper.WithinEpsilon(interPoint, segment.Start, Epsilon) ||
                        MathHelper.WithinEpsilon(interPoint, segment.End, Epsilon))
                    {
                        continue;
                    }

                    var index = tempSegments.IndexOf(segment);
                    tempSegments.RemoveAt(index);
                    var seg1 = new LineSegment2D(segment.Start, interPoint);
                    //if (ContainsInInterPointsKeepHoles(seg1, selfIntersectionPoints, FillRule, PolygonsCopy))
                    {
                        if (InsertSegment(tempSegments, seg1, index))
                        {
                            index++;
                        }
                    }

                    var seg2 = new LineSegment2D(interPoint, segment.End);
                    //if (ContainsInInterPointsKeepHoles(seg2, selfIntersectionPoints, FillRule, PolygonsCopy))
                    {
                        if (InsertSegment(tempSegments, seg2, index))
                        {
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
        internal void UpdatePolygonUsingRayInterPoints(List<Vector2D> intersectionPoints)
        {
            if (intersectionPoints.Count == 0)
            {
                return;
            }

            var tempSegments = new List<LineSegment2D>(MergedSegments);
            for (var i = 0; i < intersectionPoints.Count; i++)
            {
                var interPoint = intersectionPoints[i];
                if (!GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
                for (var j = 0; j < segments.Count; j++)
                {
                    var segment = segments[j];
                    if (MathHelper.WithinEpsilon(interPoint, segment.Start, Epsilon) ||
                        MathHelper.WithinEpsilon(interPoint, segment.End, Epsilon))
                    {
                        continue;
                    }

                    var index = tempSegments.IndexOf(segment);
                    tempSegments.RemoveAt(index);
                    var seg1 = new LineSegment2D(segment.Start, interPoint);
                    if (InsertSegment(tempSegments, seg1, index))
                    {
                        index++;
                    }

                    var seg2 = new LineSegment2D(interPoint, segment.End);
                    InsertSegment(tempSegments, seg2, index);
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
            mergedPointsHash.Clear();
            for (var i = 0; i < MergedSegments.Count; ++i)
            {
                var segment = MergedSegments[i];
                if (!mergedPointsHash.Contains(segment.Start))
                {
                    MergedPoints.Add(segment.Start);
                    mergedPointsHash.Add(segment.Start);
                }

                if (!mergedPointsHash.Contains(segment.End))
                {
                    MergedPoints.Add(segment.End);
                    mergedPointsHash.Add(segment.End);
                }
            }
        }

        /// <summary>
        /// Creates a collection of segments from a collection of points
        /// </summary>
        /// <param name="points"></param>
        /// <returns>Collection of segments if at least 2 points present in collection, otherwise null</returns>
        public static List<LineSegment2D> SplitOnSegments(List<Vector2D> points)
        {
            if (points.Count <= 1) return new List<LineSegment2D>();
            
            var segments = new List<LineSegment2D>();
            for (var i = 0; i < points.Count - 1; i++)
            {
                var segment = new LineSegment2D(points[i], points[i + 1]);
                if (!IsSameStartEnd(ref segment))
                {
                    segments.Add(segment);
                }
            }

            var seg = new LineSegment2D(points[^1], points[0]);
                
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

        /// <summary>
        /// Inserts segment in collection of segments at specifies index
        /// </summary>
        /// <param name="segments">Segments collection</param>
        /// <param name="segment2D">Segment to insert</param>
        /// <param name="index">Index to insert at</param>
        /// <returns>True if segment collection does not already has such segment, otherwise false</returns>
        public static bool InsertSegment(List<LineSegment2D> segments, LineSegment2D segment2D, int index)
        {
            if (!segments.Contains(segment2D) && !IsSameStartEnd(ref segment2D))
            {
                segments.Insert(index, segment2D);
                return true;
            }
            return false;
        }

        internal static bool IsSameStartEnd(ref LineSegment2D segment2D)
        {
            return segment2D.Start == segment2D.End;
        }

        /// <summary>
        /// Checks if two points forms single segment in collection of segments
        /// </summary>
        /// <param name="start">Start segment point</param>
        /// <param name="end">End segment point</param>
        /// <param name="segments">Collection of segments</param>
        /// <returns>True is points connected, otherwise false</returns>
        public static bool IsConnected(Vector2D start, Vector2D end, List<LineSegment2D> segments)
        {
            return IsConnected(start, end, segments, out var segment2D);
        }

        /// <summary>
        /// Checks if two points forms single segment in collection of segments and output correctly linked segment containing two points
        /// </summary>
        /// <param name="start">Start segment point</param>
        /// <param name="end">End segment point</param>
        /// <param name="segments">Collection of segments</param>
        /// <param name="correctSegment2D">Correct segment from start and end points</param>
        /// <returns>True is points connected, otherwise false</returns>
        /// <remarks>
        /// Segment could be connected not the same way as parameters. For ex. it could starts from end point and end in start point. 
        /// This method will also check for such cases and will check for segments very close to each other (less then <see cref="Epsilon"/>) 
        /// to 100% find correct segment
        /// </remarks>
        public static bool IsConnected(Vector2D start, Vector2D end, List<LineSegment2D> segments, out LineSegment2D correctSegment2D)
        {
            correctSegment2D = new LineSegment2D(start, end);
            for (var i = 0; i < segments.Count; ++i)
            {
                var currentSegment = segments[i];
                if (currentSegment.EqualsInvariant(ref correctSegment2D))
                {
                    correctSegment2D = currentSegment;
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
        public static bool GetSegmentsFromPoint(List<LineSegment2D> segments, Vector2D point, out List<LineSegment2D> foundSegments)
        {
            foundSegments = new List<LineSegment2D>();
            for (var i = 0; i < segments.Count; ++i)
            {
                var s = segments[i];
                if (Collision2D.IsPointOnSegment(ref s, ref point))
                {
                    foundSegments.Add(s);
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
            for (var i = 0; i < points.Count - 2; i++)
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
        public static bool IsConcave(List<LineSegment2D> segments)
        {
            for (var i = 0; i < segments.Count - 1; i++)
            {
                var vec0 = segments[i].DirectionNormalized;
                var vec1 = segments[i + 1].DirectionNormalized;

                var angle = MathHelper.AngleBetween((Vector3F)vec0, (Vector3F)vec1);
                if (angle > 180)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Comparer here is for sorting vectors by its Y component from smallest to biggest value
        /// </summary>
        private class VerticalVertexComparer : IComparer<Vector2D>
        {
            public static VerticalVertexComparer Default => new VerticalVertexComparer();

            public int Compare(Vector2D x, Vector2D y)
            {
                if (MathHelper.WithinEpsilon(x.Y, y.Y, Epsilon))
                {
                    return 0;
                }

                return x.Y < y.Y ? -1 : 1;
            }
        }

        public override string ToString()
        {
            return $"{Name}, PolygonItems: {Polygons.Count}";
        }

        private class PolygonPair: IEquatable<PolygonPair>
        {
            public PolygonItem Polygon1 { get; }

            public PolygonItem Polygon2 { get; }

            public ContainmentType IntersectionType { get; }

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
