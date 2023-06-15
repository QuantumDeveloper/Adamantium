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
