using System;
using System.Collections.Generic;

namespace Adamantium.Mathematics
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
        public static List<Vector3F> Triangulate(Polygon polygon)
        {
            var rays = new List<Ray2D>();
            var sortedY = new List<double>();
            var rayIntersectionPoints = new List<Vector2[]>();
            var interPoints = new List<Vector2>();
            
            var sortedList = polygon.SortVertices();
            var ray = new Ray2D(Vector2.Zero, Vector2.UnitX);
            var mostLeftPoint = polygon.HighestPoint.X;
            for (var i = 0; i < sortedList.Count; ++i)
            {
                var point = sortedList[i];
                if (sortedY.Contains(point.Y) || IsSimilarTo(point.Y, sortedY))
                {
                    continue;
                }

                sortedY.Add(point.Y);
                ray.Origin = new Vector2(mostLeftPoint, point.Y);
                rays.Add(ray);

                var rayPoints = new List<Vector2>();
                for (var j = 0; j < polygon.MergedSegments.Count; ++j)
                {
                    var segment = polygon.MergedSegments[j];
                    if (!Collision2D.RaySegmentIntersection(ref ray, ref segment, out var interPoint)) continue;
                    
                    // We need to filter points very close to each other to avoid producing incorrect results during generation of triangles
                    
                    if (IsXPointSimilarTo(interPoint, rayPoints)) continue;
                    
                    if (!IsSimilarTo(interPoint, interPoints) && !IsSimilarTo(interPoint, polygon.MergedPoints))
                    {
                        interPoints.Add(interPoint);
                    }
                    //Ray points should be added here because they needed for rayIntersectionPoints and if this collection will be empty
                    //it will affect triangulation results
                    rayPoints.Add(interPoint);
                }

                rayPoints.Sort(VertexHorizontalComparer.Defaut);
                rayIntersectionPoints.Add(rayPoints.ToArray());
            }

            polygon.UpdatePolygonUsingRayInterPoints(interPoints);

            var finalTriangles = new List<Vector3F>();
            for (var i = 0; i < rays.Count - 1; ++i)
            {
                var upperInterPoints = rayIntersectionPoints[i];
                var lowerInterPoints = rayIntersectionPoints[i + 1];

                //find all connected segments which will represent start and end of triangulation sequence
                var startEndSegments = new List<LineSegment2D>();
                for (var j = 0; j < upperInterPoints.Length; j++)
                {
                    for (var k = 0; k < lowerInterPoints.Length; k++)
                    {
                        var upperPoint = upperInterPoints[j];
                        var lowerPoint = lowerInterPoints[k];
                        if (Polygon.IsConnected(upperPoint, lowerPoint, polygon.MergedSegments))
                        {
                            startEndSegments.Add(new LineSegment2D(upperPoint, lowerPoint));
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

        /// <summary>
        /// Check if Y point difference between another in list is near <see cref="Polygon.Epsilon"/> value
        /// </summary>
        private static bool IsXPointSimilarTo(Vector2 point, List<Vector2> lst)
        {
            for (var i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point.X, lst[i].X, Polygon.Epsilon))
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
        private static void CreateTriangles(List<Vector3F> trianglesList, LineSegment2D startSegment2D, LineSegment2D endSegment2D)
        {
            if (MathHelper.IsClockwise(startSegment2D.Start, endSegment2D.Start, endSegment2D.End, Vector3.BackwardRH))
            {
                trianglesList.Add((Vector3F)startSegment2D.Start);
                trianglesList.Add((Vector3F)endSegment2D.Start);
                trianglesList.Add((Vector3F)endSegment2D.End);
            }

            if (MathHelper.IsClockwise(startSegment2D.Start, endSegment2D.End, startSegment2D.End, Vector3.BackwardRH))
            {
                //Second triangle
                trianglesList.Add((Vector3F)startSegment2D.Start);
                trianglesList.Add((Vector3F)endSegment2D.End);
                trianglesList.Add((Vector3F)startSegment2D.End);
            }
        }

        /// <summary>
        /// Sort points vertically to form correct points sequence for triangulation 
        /// </summary>
        private class VertexHorizontalComparer : IComparer<Vector2>
        {
            public static VertexHorizontalComparer Defaut => new VertexHorizontalComparer();

            public int Compare(Vector2 x, Vector2 y)
            {
                if (MathHelper.WithinEpsilon(x.X, y.X, Polygon.Epsilon))
                {
                    return 0;
                }

                return x.X < y.X ? -1 : 1;
            }
        }

    }
}
