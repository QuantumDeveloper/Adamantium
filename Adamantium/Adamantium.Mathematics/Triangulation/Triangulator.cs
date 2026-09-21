using System.Collections.Generic;

namespace Adamantium.Mathematics.Triangulation
{
    internal class Triangulator
    {
        /*
         Difference between triangulation rules

        Non-zero
        ____________
        |           |
        |           |
        |           |________
        |                   |
        |                   |
        |_____              |
              |             |
              |             |
              |_____________|


        Even-odd
        ____________
        |           |
        |           |
        |     ______|________
        |     |     |       |
        |     |     |       |
        |_____|_____|       |
              |             |
              |             |
              |_____________|
        */
        /// <summary>
        /// Triangulate given <see cref="Polygon"/> by raycasting algorithm.
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        /// <remarks>
        /// First all point in <see cref="Polygon"/> should be sorted from left to right. 
        /// After that you need to cast rays on each point from the highest point in <see cref="Polygon"/> vertically down.
        /// This will produce a collection of trapezoids where each 4 points (2 from first ray and 2 from next) will create 2 triangles.
        /// Further according to triangulation rule you should fill <see cref="Polygon"/> with triangles.
        /// According to Even-Odd rule, when you first found an intersection, you enter in polygon, on the second time - you leave a polygon and should
        /// fill it only between even and odd segment pairs (Zero is also even number)
        /// According to Non-Zero rule you should fill also self intersecting parts of polygon in addition to written above.
        /// </remarks>
        public static List<Vector3> Triangulate(Polygon polygon)
        {
            var additionalRayIntersections = new Dictionary<GeometrySegment, SortedList<double, GeometryIntersection>>();
            
            var rays = new List<Ray2D>();
            var sortedY = new List<double>();
            var sortedYHashSet = new HashSet<double>();
            var raysIntersectionPoints = new List<GeometryIntersection[]>();
            var interPoints = new List<Vector2>();
            
            var verticallySortedPointList = polygon.SortPoints();
            var ray = new Ray2D(Vector2.Zero, Vector2.UnitX);
            var leftmostXCoord = polygon.LeftmostXCoord;
            for (var i = 0; i < verticallySortedPointList.Count; ++i)
            {
                var point = verticallySortedPointList[i];
                if (sortedYHashSet.Contains(point.Coordinates.Y) || IsSimilarTo(point.Coordinates.Y, sortedY))
                {
                    continue;
                }

                sortedY.Add(point.Coordinates.Y);
                sortedYHashSet.Add(point.Coordinates.Y);
                ray.Origin = new Vector2(leftmostXCoord, point.Coordinates.Y);
                rays.Add(ray);

                var rayPoints = new List<GeometryIntersection>();
                for (var j = 0; j < polygon.MergedSegments.Count; ++j)
                {
                    var segment = polygon.MergedSegments[j];

                    if (!Collision2D.RaySegmentIntersection(ref ray, segment, out var interPoint)) continue;
                    
                    // We need to filter points very close to each other to avoid producing incorrect results during generation of triangles
                    if (IsXPointSimilarTo(interPoint, rayPoints) || IsSimilarTo(interPoint, interPoints)) continue;

                    if (!IsSimilarTo(interPoint, polygon.MergedPoints, out var geometryIntersection)/* && !IsSimilarTo(interPoint, interPoints)*/)
                    {
                        interPoints.Add(interPoint);
                        geometryIntersection = new GeometryIntersection(interPoint);

                        var distanceToStart = (interPoint - segment.Start).Length();

                        if (!additionalRayIntersections.ContainsKey(segment))
                        {
                            additionalRayIntersections[segment] = new SortedList<double, GeometryIntersection>();
                        }

                        additionalRayIntersections[segment].Add(distanceToStart, geometryIntersection);
                    }

                    //Ray points should be added here because they needed for rayIntersectionPoints and if this collection will be empty
                    //it will affect triangulation results
                    rayPoints.Add(geometryIntersection);
                }

                rayPoints.Sort(VertexGeometryHorizontalComparer.Defaut);
                raysIntersectionPoints.Add(rayPoints.ToArray());
            }

            foreach (var rayPoints in raysIntersectionPoints)
            {
                foreach (var rayPoint in rayPoints)
                {
                    rayPoint.Coordinates = Vector2.Round(rayPoint.Coordinates, 4);
                }
            }
            
            polygon.UpdatePolygonUsingAdditionalRayInterPoints(additionalRayIntersections);

            var finalTriangles = new List<Vector3>();
            for (var i = 0; i < rays.Count - 1; ++i)
            {
                var upperInterPoints = raysIntersectionPoints[i];
                var lowerInterPoints = raysIntersectionPoints[i + 1];

                //find all connected segments which will represent start and end of triangulation sequence
                var startEndSegments = new List<GeometrySegment>();
                for (var j = 0; j < upperInterPoints.Length; j++)
                {
                    for (var k = 0; k < lowerInterPoints.Length; k++)
                    {
                        var upperPoint = upperInterPoints[j];
                        var lowerPoint = lowerInterPoints[k];

                        foreach (var segment in upperPoint.ConnectedSegments)
                        {
                            if (segment.GetOtherEnd(upperPoint) == lowerPoint)
                            {
                                startEndSegments.Add(new GeometrySegment(null,upperPoint, lowerPoint));
                                break;
                            }
                        }
                    }
                }

                if (startEndSegments.Count <= 1) continue;
                
                for (var x = 0; x < startEndSegments.Count - 1; x++)
                {
                    var startSegment = startEndSegments[x];
                    var endSegment = startEndSegments[x + 1];

                    if (x % 2 == 0)
                    {
                        CreateTriangles(finalTriangles, startSegment, endSegment);
                    }
                }
            }

            return finalTriangles;
        }

        //Check if such (or near) point is already present in list
        private static bool IsSimilarTo(double point, List<double> lst)
        {
            for (var i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point, lst[i], Polygon.Epsilon))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSimilarTo(Vector2 point, List<Vector2> lst)
        {
            for (var i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point, lst[i], Polygon.Epsilon))
                {
                    return true;
                }
            }
            return false;
        }
        
        private static bool IsSimilarTo(Vector2 point, List<GeometryIntersection> lst, out GeometryIntersection similarPoint)
        {
            similarPoint = null;
            
            for (var i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point, lst[i].Coordinates, Polygon.Epsilon))
                {
                    similarPoint = lst[i];
                    return true;
                }
            }

            return false;
        }

        private static bool IsXPointSimilarTo(Vector2 point, List<GeometryIntersection> lst)
        {
            for (var i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point.X, lst[i].Coordinates.X, Polygon.Epsilon))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Add 3 points to collection if their order is clockwise.
        /// </summary>
        /// <param name="trianglesList"></param>
        /// <param name="startSegment2D"></param>
        /// <param name="endSegment2D"></param>
        private static void CreateTriangles(List<Vector3> trianglesList, GeometrySegment startSegment2D, GeometrySegment endSegment2D)
        {
            if (MathHelper.IsClockwise(startSegment2D.Start, endSegment2D.Start, endSegment2D.End, Vector3.BackwardRH))
            {
                trianglesList.Add((Vector3)startSegment2D.Start);
                trianglesList.Add((Vector3)endSegment2D.Start);
                trianglesList.Add((Vector3)endSegment2D.End);
            }

            if (MathHelper.IsClockwise(startSegment2D.Start, endSegment2D.End, startSegment2D.End, Vector3.BackwardRH))
            {
                //Second triangle
                trianglesList.Add((Vector3)startSegment2D.Start);
                trianglesList.Add((Vector3)endSegment2D.End);
                trianglesList.Add((Vector3)startSegment2D.End);
            }
        }

        /// <summary>
        /// Triangulates a CONVEX simple polygon as a fan in O(n): triangles (v0, vi, vi+1). Each triangle is
        /// emitted clockwise (same convention as <see cref="CreateTriangles"/>) so winding stays consistent.
        /// Caller must guarantee convexity (see <see cref="MathHelper.IsConvex"/>).
        /// </summary>
        public static List<Vector3> FanTriangulate(IReadOnlyList<Vector2> points)
        {
            var triangles = new List<Vector3>();
            for (var i = 1; i + 1 < points.Count; i++)
            {
                AddTriangleClockwise(triangles, points[0], points[i], points[i + 1]);
            }
            return triangles;
        }

        /// <summary>
        /// Triangulates a single SIMPLE (non-self-intersecting) closed contour via earcut in ~O(n), producing
        /// ~n-2 triangles (no slivers). Triangles are emitted clockwise (same convention as the scanline path).
        /// </summary>
        public static List<Vector3> EarcutTriangulate(IReadOnlyList<Vector2> ring)
        {
            var triangles = new List<Vector3>();
            int n = ring.Count;
            if (n < 3) return triangles;

            var data = new double[n * 2];
            for (var i = 0; i < n; i++)
            {
                data[i * 2] = ring[i].X;
                data[i * 2 + 1] = ring[i].Y;
            }

            var indices = Earcut.Tessellate(data, null, 2);
            for (var t = 0; t + 2 < indices.Count; t += 3)
            {
                AddTriangleClockwise(triangles, ring[indices[t]], ring[indices[t + 1]], ring[indices[t + 2]]);
            }
            return triangles;
        }

        /// <summary>
        /// Triangulates an outer ring with holes via earcut (holes cut out). All rings must be simple and
        /// non-crossing (caller guarantees). Triangles are emitted clockwise.
        /// </summary>
        public static List<Vector3> EarcutWithHoles(IReadOnlyList<Vector2> outer, IReadOnlyList<IReadOnlyList<Vector2>> holes)
        {
            var triangles = new List<Vector3>();
            if (outer.Count < 3) return triangles;

            var all = new List<Vector2>(outer);
            int[] holeIndices = null;
            if (holes != null && holes.Count > 0)
            {
                var starts = new List<int>();
                foreach (var hole in holes)
                {
                    if (hole.Count < 3) continue;
                    starts.Add(all.Count);
                    all.AddRange(hole);
                }
                if (starts.Count > 0) holeIndices = starts.ToArray();
            }

            var data = new double[all.Count * 2];
            for (var i = 0; i < all.Count; i++)
            {
                data[i * 2] = all[i].X;
                data[i * 2 + 1] = all[i].Y;
            }

            var indices = Earcut.Tessellate(data, holeIndices, 2);
            for (var t = 0; t + 2 < indices.Count; t += 3)
            {
                AddTriangleClockwise(triangles, all[indices[t]], all[indices[t + 1]], all[indices[t + 2]]);
            }
            return triangles;
        }

        private static void AddTriangleClockwise(List<Vector3> triangles, Vector2 a, Vector2 b, Vector2 c)
        {
            if (MathHelper.IsClockwise(a, b, c, Vector3.BackwardRH))
            {
                triangles.Add((Vector3)a);
                triangles.Add((Vector3)b);
                triangles.Add((Vector3)c);
            }
            else
            {
                triangles.Add((Vector3)a);
                triangles.Add((Vector3)c);
                triangles.Add((Vector3)b);
            }
        }

        private class VertexGeometryHorizontalComparer : IComparer<GeometryIntersection>
        {
            public static VertexGeometryHorizontalComparer Defaut => new ();

            public int Compare(GeometryIntersection x, GeometryIntersection y)
            {
                if (MathHelper.WithinEpsilon(x.Coordinates.X, y.Coordinates.X, Polygon.Epsilon))
                {
                    return 0;
                }

                return x.Coordinates.X < y.Coordinates.X ? -1 : 1;
            }
        }

    }
}
