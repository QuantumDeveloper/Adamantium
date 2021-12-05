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
        private readonly HashSet<Vector2> mergedPointsHash;
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
        public List<Vector2> MergedPoints { get; private set; }

        /// <summary>
        /// Collection of segments from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<LineSegment2D> MergedSegments { get; private init; }

        /// <summary>
        /// Collection of self intersected points from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<Vector2> SelfIntersectedPoints { get; private init; }

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
        public Vector2 HighestPoint;

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
            MergedPoints = new List<Vector2>();
            mergedPointsHash = new HashSet<Vector2>();
            MergedSegments = new List<LineSegment2D>();
            SelfIntersectedPoints = new List<Vector2>();
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

        public List<Vector3F> FIllDirect(List<Vector2> points, List<LineSegment2D> segments)
        {
            MergedPoints.AddRange(points);
            MergedSegments.AddRange(segments);
            UpdateBoundingBox();
            
            var result = Triangulator.Triangulate(this);
            return result;
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

            var edgePoints = new List<Vector2>();
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

                    //Check if polygons are inside each other
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

                // If Non-zero, lets find a list of points describing all possible
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
        private void RemoveInternalSegments(List<Vector2> interPoints)
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

                    if (PolygonHelper.IsConnectedInvariant(point1, point2, MergedSegments, out var segment2D))
                    {
                        MergedSegments.Remove(segment2D);
                    }
                }
            }
            UpdateMergedPoints();
        }

        private bool FindEdgeIntersectionPoints(PolygonItem polygon1, PolygonItem polygon2, out List<Vector2> edgePoints)
        {
            edgePoints = new List<Vector2>();
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
        private List<Vector2> FindAllPossibleIntersections(PolygonItem polygon1, PolygonItem polygon2, List<Vector2> edgePoints)
        {
            var allIntersections = new List<Vector2>(edgePoints);

            for (var i = 0; i < polygon1.Points.Count; i++)
            {
                var point = polygon1.Points[i];
                if (Collision2D.IsPointInsideArea(point, polygon2.Segments) &&
                    !allIntersections.Contains(point))
                {
                    allIntersections.Add(point);
                }
            }
            for (var i = 0; i < polygon2.Points.Count; i++)
            {
                var point = polygon2.Points[i];
                if (Collision2D.IsPointInsideArea(point, polygon1.Segments) &&
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
        public List<Vector2> SortPoints()
        {
            var sortedList = new List<Vector2>(MergedPoints);
            sortedList.Sort(VerticalPointsComparer.Default);
            return sortedList;
        }

        private void UpdateBoundingBox()
        {
            if (MergedPoints.Count <= 0) return;
            var minimum = new Vector2(double.MaxValue);
            var maximum = new Vector2(double.MinValue);

            for (var i = 0; i < MergedPoints.Count; ++i)
            {
                var point = MergedPoints[i];
                Vector2.Min(ref minimum, ref point, out minimum);
                Vector2.Max(ref maximum, ref point, out maximum);
            }
            //HighestPoint = new Vector2D(minimum.X, maximum.Y);
            HighestPoint = new Vector2(minimum.X, minimum.Y);
        }

        /// <summary>
        /// Updates segments for polygons using self Intersection points
        /// </summary>
        /// <param name="selfIntersectionPoints">Self intersection points</param>
        private void UpdatePolygonUsingSelfPoints(List<Vector2> selfIntersectionPoints)
        {
            if (selfIntersectionPoints.Count == 0)
            {
                return;
            }

            var tempSegments = new List<LineSegment2D>(MergedSegments);
            for (var i = 0; i < selfIntersectionPoints.Count; i++)
            {
                var interPoint = selfIntersectionPoints[i];
                if (!PolygonHelper.GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
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
                    if (PolygonHelper.InsertSegment(tempSegments, seg1, index))
                    {
                        index++;
                    }

                    var seg2 = new LineSegment2D(interPoint, segment.End);
                    PolygonHelper.InsertSegment(tempSegments, seg2, index);
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
        internal void UpdatePolygonUsingRayInterPoints(List<Vector2> intersectionPoints)
        {
            if (intersectionPoints.Count == 0)
            {
                return;
            }

            var tempSegments = new List<LineSegment2D>(MergedSegments);
            for (var i = 0; i < intersectionPoints.Count; i++)
            {
                var interPoint = intersectionPoints[i];
                if (!PolygonHelper.GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
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
                    if (PolygonHelper.InsertSegment(tempSegments, seg1, index))
                    {
                        index++;
                    }

                    var seg2 = new LineSegment2D(interPoint, segment.End);
                    PolygonHelper.InsertSegment(tempSegments, seg2, index);
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
        /// Comparer here is for sorting vectors by its Y component from smallest to biggest value
        /// </summary>
        private class VerticalPointsComparer : IComparer<Vector2>
        {
            public static VerticalPointsComparer Default => new VerticalPointsComparer();

            public int Compare(Vector2 x, Vector2 y)
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
