using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Adamantium.Mathematics.Triangulation
{
    /// <summary>
    /// Describes a collection of <see cref="MeshContour"/>s, which will form a single merged triangulated polygon
    /// </summary>
    public class Polygon
    {
        private readonly HashSet<Vector2> mergedPointsHash;
        private readonly List<ContoursContainer> contourContainers;

        public List<MeshContour> ProcessedContours { get; private set; }

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
        /// Collection of points from all <see cref="MeshContour"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<GeometryIntersection> MergedPoints { get; private set; }

        /// <summary>
        /// Collection of segments from all <see cref="MeshContour"/>s in current <see cref="Polygon"/>
        /// </summary>
        public List<GeometrySegment> MergedSegments { get; private set; }

        /// <summary>
        /// Leftmost X coordinate for polygon 
        /// </summary>
        public double LeftmostXCoord;

        /// <summary>
        /// Triangulation rule (true - Even-Odd, false - Non-Zero)
        /// </summary>
        public FillRule FillRule { get; set; }

        private readonly List<ContourPair> checkedContours;
        private readonly object vertexLocker = new object();

        /// <summary>
        /// Constructs <see cref="Polygon"/>
        /// </summary>
        public Polygon(FillRule fillRule = FillRule.EvenOdd)
        {
            contourContainers = new List<ContoursContainer>();
            MergedPoints = new List<GeometryIntersection>();
            mergedPointsHash = new HashSet<Vector2>();
            MergedSegments = new List<GeometrySegment>();
            checkedContours = new List<ContourPair>();
            ProcessedContours = new List<MeshContour>();
            FillRule = fillRule;
        }

        public void AddContour(MeshContour contour, bool newContainer = true)
        {
            if (newContainer) contourContainers.Add(new ContoursContainer());
            contourContainers.Last().AddContour(contour);
        }

        public void AddContours(params MeshContour[] contours)
        {
            foreach (var contour in contours)
            {
                contourContainers.Add(new ContoursContainer());
                contourContainers.Last().AddContour(contour);
            }
        }

        /// <summary>
        /// Triangulate current <see cref="Polygon"/>
        /// </summary>
        /// <returns></returns>
        public List<Vector3> FillIndirect(bool triangulate = true)
        {
            var localContourContainers = new List<ContoursContainer>();

            foreach (var container in contourContainers)
            {
                localContourContainers.Add(container.Copy());
            }
            
            Parallel.For(0, localContourContainers.Count, i =>
            {
                localContourContainers[i].SplitContoursOnSegments();
                localContourContainers[i].RemoveContoursSelfIntersections(FillRule);
            });
            
            // process non intersecting contours - only when the whole container not intersects with other containers and contains only single contour
            // such container is removed from list
            var nonIntersectedContours = GetNonIntersectingContours(localContourContainers);
            
            var vertices = new List<Vector3>();
            
            if (triangulate)
            {
                if (nonIntersectedContours.Count > 0)
                {
                    ProcessedContours.AddRange(nonIntersectedContours);
                    
                    Parallel.ForEach(nonIntersectedContours, item =>
                    {
                        var result1 = TriangulateContour(item);
                        lock (vertexLocker)
                        {
                            vertices.AddRange(result1);
                        }
                    });
                }
            }
            
            // process intersecting contours
            if (localContourContainers.Count > 0)
            {
                MergeContours(localContourContainers);
                
                foreach (var container in localContourContainers)
                {
                    ProcessedContours.AddRange(container.Contours);
                }

                UpdateLeftmostXCoord();
            
                if (triangulate)
                {
                    var result = Triangulator.Triangulate(this);
                    lock (vertexLocker)
                    {
                        vertices.AddRange(result);
                    }
                }
            }
            
            return vertices;
        }

        public List<Vector3> FillDirect(List<GeometryIntersection> points, List<GeometrySegment> segments)
        {
            MergedPoints.AddRange(points);
            MergedSegments.AddRange(segments);
            UpdateLeftmostXCoord();

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

            polygon.UpdateLeftmostXCoord();

            return Triangulator.Triangulate(polygon);
        }

        private List<MeshContour> GetNonIntersectingContours(List<ContoursContainer> localContourContainers)
        {
            var contourList = new List<MeshContour>();

            if (localContourContainers.Count == 0) return contourList;

            if (localContourContainers.Count == 1 && localContourContainers[0].Contours.Count == 1)
            {
                contourList.Add(localContourContainers[0].Contours[0]);
                return contourList;
            }

            foreach (var container1 in localContourContainers)
            {
                var canAdd = true;

                foreach (var container2 in localContourContainers)
                {
                    if (container1 == container2) continue;

                    var containment = container1.BoundingBox.Intersects(container2.BoundingBox);

                    if (containment || container1.Contours.Count > 1)
                    {
                        canAdd = false;
                        break;
                    }
                }

                if (canAdd)
                {
                    contourList.Add(container1.Contours[0]);
                    container1.ClearContours();
                }
            }

            for (var i = 0; i < localContourContainers.Count; i++)
            {
                if (localContourContainers[i].Contours.Count == 0) localContourContainers.RemoveAt(i);
            }

            return contourList;
        }

        /// <summary>
        /// Merge points from all <see cref="MeshContour"/>s
        /// </summary>
        private void MergePoints()
        {
            MergedPoints.Clear();
            
            var pointsHashSet = new HashSet<GeometryIntersection>();

            foreach (var segment in MergedSegments)
            {
                foreach (var end in segment.SegmentEnds)
                {
                    if (!pointsHashSet.Contains(end))
                    {
                        pointsHashSet.Add(end);
                        MergedPoints.Add(end);
                    }
                }
            }
        }

        /// <summary>
        /// Merge segments from all <see cref="MeshContour"/>s
        /// </summary>
        private void MergeSegments(List<ContoursContainer> localContourContainers)
        {
            MergedSegments.Clear();

            foreach (var container in localContourContainers)
            {
                foreach (var contour in container.Contours)
                {
                    MergedSegments.AddRange(contour.Segments);
                }
            }
        }

        private void MergeContours(List<ContoursContainer> localContourContainers)
        {
            if (localContourContainers.Count <= 1)
            {
                MergeSegments(localContourContainers);
                MergePoints();

                return;
            }
            
            var processedSegments = new List<GeometrySegment>();

            // each iteration we are processing next contour with the merged segments of all previous contours 
            for (var i = 1; i < localContourContainers.Count; i++)
            {
                processedSegments.Clear();

                for (var j = 0; j < i; j++)
                {
                    processedSegments.AddRange(localContourContainers[j].MergeContoursSegments());
                }
                
                var nextSegments = new List<GeometrySegment>();
                nextSegments.AddRange(localContourContainers[i].MergeContoursSegments());

                ContourProcessingHelper.ProcessContoursIntersections(processedSegments, nextSegments);
            }

            // update for correct marking
            foreach (var container in localContourContainers)
            {
                container.UpdateContoursPoints();
            }

            if (FillRule == FillRule.NonZero)
            {
                var arguableSegments = new List<GeometrySegment>();

                foreach (var checkedContainer in localContourContainers)
                {
                    foreach (var container in localContourContainers)
                    {
                        if (container == checkedContainer) continue;
                        
                        arguableSegments.AddRange(ContourProcessingHelper.MarkSegments(container.MergeContoursSegments(), checkedContainer.MergeContoursSegments()));
                    }
                }

                if (arguableSegments.Count > 0)
                {
                    // resolve arguable segments as inner or outer
                    var mergedSegments = new List<GeometrySegment>();

                    foreach (var container in localContourContainers)
                    {
                        mergedSegments.AddRange(container.MergeContoursSegments());
                    }

                    ContourProcessingHelper.ResolveArguableSegments(arguableSegments, mergedSegments);
                }

                // remove only inner segments, arguable segments will be skipped
                foreach (var container in localContourContainers)
                {
                    container.RemoveSegmentsByRule(true);
                }

                if (arguableSegments.Count > 0)
                {
                    // additionally remove resolved segments if they are inner
                    foreach (var arguableSeg in arguableSegments)
                    {
                        if (arguableSeg.IsInner)
                        {
                            arguableSeg.RemoveSelfFromConnectedSegments();
                            arguableSeg.RemoveSelfFromParent();
                        }
                    }

                    foreach (var container in localContourContainers)
                    {
                        container.UpdateContoursPoints();
                    }
                }
            }

            MergeSegments(localContourContainers);
            MergePoints();
        }

        /// <summary>
        /// Adds polygons to checklist to not process this polygon pair again
        /// </summary>
        /// <param name="polygon1">First polygon</param>
        /// <param name="polygon2">Second polygon</param>
        /// <param name="intersectionType">Intersection type between two polygons</param>
        private void AddContourPairToList(MeshContour polygon1, MeshContour polygon2,
            ContainmentType intersectionType)
        {
            checkedContours.Add(new ContourPair(polygon1, polygon2, intersectionType));
        }

        /// <summary>
        /// Check does 2 polygons present in check list
        /// </summary>
        /// <param name="contour1">First polygon</param>
        /// <param name="polygon2">Second polygon</param>
        /// <returns>True if polygons already contains in check list, otherwise false</returns>
        private bool ContainsContourPair(MeshContour contour1, MeshContour contour2)
        {
            return checkedContours.Contains(new ContourPair(contour1, contour2));
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

        private void UpdateLeftmostXCoord()
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
            if (additionalRayIntersections.Count == 0) return;
            
            foreach (var pair in additionalRayIntersections)
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }

                pair.Key.RemoveSelfFromConnectedSegments();
                if (MergedSegments.Contains(pair.Key)) MergedSegments.Remove(pair.Key);

                var startPart = new GeometrySegment(pair.Key.Parent, pair.Key.SegmentEnds[0], pair.Value.Values.First());
                MergedSegments.Add(startPart);

                for (var i = 0; i < pair.Value.Values.Count - 1; i++)
                {
                    var int1 = pair.Value.Values[i];
                    var int2 = pair.Value.Values[i + 1];

                    var seg = new GeometrySegment(pair.Key.Parent, int1, int2);
                    MergedSegments.Add(seg);
                }

                var endPart = new GeometrySegment(pair.Key.Parent, pair.Value.Values.Last(), pair.Key.SegmentEnds[1]);
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
            var contoursCnt = contourContainers.Sum(container => container.Contours.Count);

            return $"{Name}, Contour containers: {contourContainers.Count}, Mesh contours: {contoursCnt}";
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