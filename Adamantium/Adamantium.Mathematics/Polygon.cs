using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Adamantium.Mathematics
{
    /// <summary>
    /// Describes a collection of <see cref="PolygonItem"/>s, which will form a single merged triangulated polygon
    /// </summary>
    public class Polygon
    {
        private readonly HashSet<Vector2> mergedPointsHash;
        private readonly List<MeshContour> contours;

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
        /// Collection of <see cref="MeshContour"/>
        /// </summary>
        public IReadOnlyCollection<MeshContour> Contours => contours.AsReadOnly();

        /// <summary>
        /// Collection of points from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<GeometryIntersection> MergedPoints { get; private set; }

        /// <summary>
        /// Collection of segments from all <see cref="PolygonItem"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<GeometrySegment> MergedSegments { get; private init; }

        /// <summary>
        /// Leftmost X coordinate for polygon 
        /// </summary>
        public double LeftmostXCoord;

        /// <summary>
        /// Triangulation rule (true - Even-Odd, false - Non-Zero)
        /// </summary>
        public FillRule FillRule { get; set; }

        private readonly List<ContourPair> checkedPolygons;
        private readonly object vertexLocker = new object();

        /// <summary>
        /// Constructs <see cref="Polygon"/>
        /// </summary>
        public Polygon()
        {
            contours = new List<MeshContour>();
            MergedPoints = new List<GeometryIntersection>();
            mergedPointsHash = new HashSet<Vector2>();
            MergedSegments = new List<GeometrySegment>();
            checkedPolygons = new List<ContourPair>();
            FillRule = FillRule.EvenOdd;
        }

        public void AddItem(MeshContour item)
        {
            contours.Add(item);
        }

        public void AddItems(params MeshContour[] items)
        {
            contours.AddRange(items);
        }

        /// <summary>
        /// Triangulate current <see cref="Polygon"/>
        /// </summary>
        /// <returns></returns>
        public List<Vector3> Fill()
        {
            Parallel.For(0, contours.Count, i =>
            {
                contours[i].SplitOnSegments();
                contours[i].RemoveSelfIntersections(FillRule);
            });

            // process non intersecting contours
            var nonIntersectedContours = GetNonIntersectingContours();

            var vertices = new List<Vector3>();

            if (nonIntersectedContours.Count > 0)
            {
                Parallel.ForEach(nonIntersectedContours, item =>
                {
                    var result1 = TriangulateContour(item);
                    lock (vertexLocker)
                    {
                        vertices.AddRange(result1);
                    }
                });
            }

            // process intersecting contours
            if (contours.Count > 0)
            {
                MergeContours();

                UpdateBoundingBox();

                var result = Triangulator.Triangulate(this);
                lock (vertexLocker)
                {
                    vertices.AddRange(result);
                }
            }

            return vertices;
        }

        public List<Vector3> FillDirect(List<GeometryIntersection> points, List<GeometrySegment> segments)
        {
            MergedPoints.AddRange(points);
            MergedSegments.AddRange(segments);
            UpdateBoundingBox();

            var result = Triangulator.Triangulate(this);
            return result;
        }

        private List<Vector3> TriangulateContour(MeshContour item)
        {
            var polygon = new Polygon
            {
                Name = Name,
                MergedPoints = item.GeometryPoints,
                MergedSegments = item.Segments
            };

            polygon.UpdateBoundingBox();

            return Triangulator.Triangulate(polygon);
        }

        private List<MeshContour> GetNonIntersectingContours()
        {
            var contourList = new List<MeshContour>();

            if (Contours.Count == 0) return contourList;

            if (Contours.Count == 1)
            {
                contourList.Add(contours[0]);
                return contourList;
            }

            foreach (var contour1 in Contours)
            {
                var canAdd = true;

                foreach (var contour2 in Contours)
                {
                    if (contour1 == contour2) continue;

                    var containment = contour1.BoundingBox.Intersects(contour2.BoundingBox);

                    if (containment)
                    {
                        canAdd = false;
                        break;
                    }
                }

                if (canAdd)
                {
                    contourList.Add(contour1);
                }
            }

            foreach (var p in contourList)
            {
                if (contours.Contains(p))
                {
                    contours.Remove(p);
                }
            }

            return contourList;
        }

        /// <summary>
        /// Merge points from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergePoints()
        {
            MergedPoints.Clear();
            for (var i = 0; i < contours.Count; ++i)
            {
                MergedPoints.AddRange(contours[i].GeometryPoints);
            }
        }

        /// <summary>
        /// Merge segments from all <see cref="PolygonItem"/>s
        /// </summary>
        private void MergeSegments()
        {
            MergedSegments.Clear();
            for (var i = 0; i < contours.Count; ++i)
            {
                MergedSegments.AddRange(contours[i].Segments);
            }
        }

        private void ProcessContoursIntersections(MeshContour meshContour1, MeshContour meshContour2)
        {
            var intersectionsList = new Dictionary<Vector2, GeometryIntersection>();
            var contour1Intersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();
            var contour2Intersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();

            foreach (var segment1 in meshContour1.Segments)
            {
                contour1Intersections[segment1] = new SortedList<double, GeometryIntersection>();

                foreach (var segment2 in meshContour2.Segments)
                {
                    contour2Intersections[segment2] = new SortedList<double, GeometryIntersection>();

                    if (Collision2D.SegmentSegmentIntersection(segment1, segment2, out var point))
                    {
                        if (point != segment1.Start && point != segment1.End)
                        {
                            if (!intersectionsList.ContainsKey(point))
                            {
                                intersectionsList[point] = new GeometryIntersection(point);
                            }

                            var distanceToStart = (point - segment1.Start).Length();
                            contour1Intersections[segment1].Add(distanceToStart, intersectionsList[point]);
                        }

                        if (point != segment2.Start && point != segment2.End)
                        {
                            if (!intersectionsList.ContainsKey(point))
                            {
                                intersectionsList[point] = new GeometryIntersection(point);
                            }

                            var distanceToStart = (point - segment2.Start).Length();
                            contour2Intersections[segment2].Add(distanceToStart, intersectionsList[point]);
                        }
                    }
                }
            }

            FillMeshSegments(contour1Intersections);
            FillMeshSegments(contour2Intersections);
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

                var startPart = new GeometrySegment(key.SegmentEnds[0], value.Values.First());
                key.Parent.Segments.Add(startPart);

                for (var i = 0; i < value.Values.Count - 1; i++)
                {
                    var int1 = value.Values[i];
                    var int2 = value.Values[i + 1];

                    var seg = new GeometrySegment(int1, int2);
                    key.Parent.Segments.Add(seg);
                }

                var endPart = new GeometrySegment(value.Values.Last(), key.SegmentEnds[1]);
                key.Parent.Segments.Add(endPart);
            }
        }
        
        private void MarkSegments(MeshContour meshContour1, MeshContour meshContour2)
        {
            var mesh1Segments = meshContour1.Segments;
            var mesh2Points = meshContour2.GeometryPoints;

            foreach (var point in mesh2Points)
            {
                var ray = new Ray2D(point.Coordinates, Vector2.UnitX);
                var intersectionCnt = 0;
            
                foreach (var segment in mesh1Segments)
                {
                    var seg = new LineSegment2D(segment);
                    if (Collision2D.RaySegmentIntersection(ref ray, ref seg, out var intPoint) && point.Coordinates != intPoint) ++intersectionCnt;
                }

                if (intersectionCnt % 2 != 0)
                {
                    foreach (var segment in point.ConnectedSegments)
                    {
                        segment.IsInnerSegment = true;
                    }
                }
            }
        }

        private void MergeContours()
        {
            if (contours.Count <= 1)
            {
                MergeSegments();
                MergePoints();

                return;
            }

            for (var i = 0; i < contours.Count; i++)
            {
                var contour1 = contours[i];
                for (var j = 0; j < contours.Count; j++)
                {
                    var contour2 = contours[j];
                    if (contour1 == contour2 || ContainsPolygonPair(contour1, contour2))
                    {
                        continue;
                    }

                    //Check if polygons are inside each other
                    var intersectionResult = contour1.IsCompletelyContains(contour2);

                    AddPolygonsPairsToList(contour1, contour2, intersectionResult);
                }
            }

            if (FillRule == FillRule.EvenOdd) return;

            foreach (var polygonPair in checkedPolygons)
            {
                var contour1 = polygonPair.Contour1;
                var contour2 = polygonPair.Contour2;
                var intersectionResult = polygonPair.IntersectionType;

                //If polygons partly inside each other
                if (intersectionResult == ContainmentType.Intersects)
                {
                    ProcessContoursIntersections(contour1, contour2);
                }
            }

            // update for correct marking
            foreach (var contour in contours)
            {
                contour.UpdatePoints();
            }
            
            foreach (var polygonPair in checkedPolygons)
            {
                var contour1 = polygonPair.Contour1;
                var contour2 = polygonPair.Contour2;
                var intersectionResult = polygonPair.IntersectionType;

                //If polygons partly inside each other
                if (intersectionResult == ContainmentType.Intersects)
                {
                    MarkSegments(contour1, contour2);
                    MarkSegments(contour2, contour1);
                }
            }

            // delete inner segments and update points 
            foreach (var contour in contours)
            {
                contour.RemoveSegmentsByRule(true);
                contour.UpdatePoints();
            }

            MergeSegments();
            MergePoints();
        }

        /// <summary>
        /// Adds polygons to checklist to not process this polygon pair again
        /// </summary>
        /// <param name="polygon1">First polygon</param>
        /// <param name="polygon2">Second polygon</param>
        /// <param name="intersectionType">Intersection type between two polygons</param>
        private void AddPolygonsPairsToList(MeshContour polygon1, MeshContour polygon2,
            ContainmentType intersectionType)
        {
            checkedPolygons.Add(new ContourPair(polygon1, polygon2, intersectionType));
        }

        /// <summary>
        /// Check does 2 polygons present in check list
        /// </summary>
        /// <param name="contour1">First polygon</param>
        /// <param name="polygon2">Second polygon</param>
        /// <returns>True if polygons already contains in check list, otherwise false</returns>
        private bool ContainsPolygonPair(MeshContour contour1, MeshContour contour2)
        {
            return checkedPolygons.Contains(new ContourPair(contour1, contour2));
        }

        /// <summary>
        /// Sort points by its X component from smallest to largest
        /// </summary>
        /// <returns>New collection of </returns>
        public List<GeometryIntersection> SortPoints()
        {
            var sortedList = new List<GeometryIntersection>(MergedPoints);
            sortedList.Sort(VerticalGeometryPointsComparer.Default);
            return sortedList;
        }

        private void UpdateBoundingBox()
        {
            if (MergedPoints.Count <= 0) return;
            var minimum = new Vector2(double.MaxValue);
            var maximum = new Vector2(double.MinValue);

            for (var i = 0; i < MergedPoints.Count; ++i)
            {
                var point = MergedPoints[i].Coordinates;
                Vector2.Min(ref minimum, ref point, out minimum);
                Vector2.Max(ref maximum, ref point, out maximum);
            }

            LeftmostXCoord = minimum.X;
        }

        /// <summary>
        /// Update <see cref="MergedSegments"/> collection based on collection of intersection points. 
        /// As a result, this method will create new additional segments nad update <see cref="MergedPoints"/> collection
        /// </summary>
        /// <param name="additionalRayIntersections">Collection of intersection points</param>
        internal void UpdatePolygonUsingAdditionalRayInterPoints(Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>> additionalRayIntersections)
        {
            foreach (var (key, value) in additionalRayIntersections)
            {
                if (value.Count == 0)
                {
                    continue;
                }

                key.RemoveSelfFromConnectedSegments();
                if (MergedSegments.Contains(key)) MergedSegments.Remove(key);

                var startPart = new GeometrySegment(key.SegmentEnds[0], value.Values.First());
                MergedSegments.Add(startPart);

                for (var i = 0; i < value.Values.Count - 1; i++)
                {
                    var int1 = value.Values[i];
                    var int2 = value.Values[i + 1];

                    var seg = new GeometrySegment(int1, int2);
                    MergedSegments.Add(seg);
                }

                var endPart = new GeometrySegment(value.Values.Last(), key.SegmentEnds[1]);
                MergedSegments.Add(endPart);
            }
        
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
                    MergedPoints.Add(segment.SegmentEnds[0]);
                    mergedPointsHash.Add(segment.Start);
                }

                if (!mergedPointsHash.Contains(segment.End))
                {
                    MergedPoints.Add(segment.SegmentEnds[1]);
                    mergedPointsHash.Add(segment.End);
                }
            }
        }

        private class VerticalGeometryPointsComparer : IComparer<GeometryIntersection>
        {
            public static VerticalGeometryPointsComparer Default => new ();

            public int Compare(GeometryIntersection x, GeometryIntersection y)
            {
                if (MathHelper.WithinEpsilon(x.Coordinates.Y, y.Coordinates.Y, Epsilon))
                {
                    return 0;
                }

                return x.Coordinates.Y < y.Coordinates.Y ? -1 : 1;
            }
        }
        
        public override string ToString()
        {
            return $"{Name}, PolygonItems: {Contours.Count}";
        }

        private class ContourPair : IEquatable<ContourPair>
        {
            public MeshContour Contour1 { get; }

            public MeshContour Contour2 { get; }

            public ContainmentType IntersectionType { get; }

            public ContourPair(MeshContour contour1, MeshContour contour2, ContainmentType intersectionType)
            {
                Contour1 = contour1;
                Contour2 = contour2;
                IntersectionType = intersectionType;
            }

            public ContourPair(MeshContour contour1, MeshContour contour2)
            {
                Contour1 = contour1;
                Contour2 = contour2;
            }

            public bool Equals(ContourPair other)
            {
                if (ReferenceEquals(null, other))
                    return false;
                if (ReferenceEquals(this, other))
                    return true;
                return (Equals(Contour1, other.Contour1) && Equals(Contour2, other.Contour2)) ||
                       (Equals(Contour1, other.Contour2) && Equals(Contour2, other.Contour1));
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                if (ReferenceEquals(this, obj))
                    return true;
                if (obj.GetType() != this.GetType())
                    return false;
                return Equals((ContourPair) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Contour1 != null ? Contour1.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Contour2 != null ? Contour2.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (int) IntersectionType;
                    return hashCode;
                }
            }

            public override string ToString()
            {
                return $"Polygon1 {Contour1}, Polygon2 {Contour2}, IntersectionType {IntersectionType}";
            }
        }
    }
}