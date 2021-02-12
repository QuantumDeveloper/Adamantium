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
        /// Accroding to Even-Odd rule, when you first found an intersection, you enter in polygon, on the second time - you leave a polygon and should
        /// fill it only between even and odd segment pairs (Zero is also even number)
        /// According to Non-Zero rule you should fill also self intersecting parts of polygon in addition to written above.
        /// </remarks>
        public static List<Vector3F> Triangulate(Polygon polygon)
        {
            var rays = new List<Ray2D>();
            var sortedX = new List<double>();
            var rayIntersectionPoints = new List<Vector2D[]>();
            List<Vector2D> interPoints = new List<Vector2D>();

            var sortedList = polygon.SortVertices();
            var ray = new Ray2D(Vector2D.Zero, Vector2D.UnitX);
            var highestPoint = polygon.HighestPoint.X;
            for (int i = 0; i < sortedList.Count; ++i)
            {
                var point = sortedList[i];
                if (sortedX.Contains(point.Y) || IsSimilarTo(point.Y, sortedX))
                {
                    continue;
                }

                sortedX.Add(point.Y);
                Vector2D rayOrigin = new Vector2D(highestPoint, point.Y);
                ray.Origin = rayOrigin;
                rays.Add(ray);

                List<Vector2D> rayPoints = new List<Vector2D>();
                for (int j = 0; j < polygon.MergedSegments.Count; ++j)
                {
                    var segment = polygon.MergedSegments[j];
                    Vector2D interPoint;
                    if (Collision2D.RaySegmentIntersection(ref ray, ref segment, out interPoint))
                    {
                        // We need to filter points very close to each other to avoid producing incorrect results during generation of triangles
                        if (!IsXPointSimilarTo(interPoint, rayPoints))
                        {
                            if (!IsSimilarTo(interPoint, interPoints) && !IsSimilarTo(interPoint, polygon.MergedPoints))
                            {
                                interPoints.Add(interPoint);
                            }
                            //Ray points should be added here because they needed for rayIntersectionPoints and if this collection will be empty
                            //it will affect triangulation results
                            rayPoints.Add(interPoint);
                        }
                    }
                }

                rayPoints.Sort(VertexHorizontalComparer.Defaut);
                rayIntersectionPoints.Add(rayPoints.ToArray());
            }

            polygon.UpdatePolygonUsingRayInterPoints(interPoints);

            List<Vector3F> finalTriangles = new List<Vector3F>();
            for (int i = 0; i < rays.Count - 1; ++i)
            {
                if (i == 5)
                {
                    int x = 0;
                }
                
                var upperInterPoints = rayIntersectionPoints[i];
                var lowerInterPoints = rayIntersectionPoints[i + 1];

                //find all connected segments which will represent start and end of triangulation sequence
                List<LineSegment> startEndSegmnets = new List<LineSegment>();
                for (int j = 0; j < upperInterPoints.Length; j++)
                {
                    for (int k = 0; k < lowerInterPoints.Length; k++)
                    {
                        if (Polygon.IsConnected(upperInterPoints[j], lowerInterPoints[k], polygon.MergedSegments))
                        {
                            startEndSegmnets.Add(new LineSegment(upperInterPoints[j], lowerInterPoints[k]));
                        }
                    }
                }

                if (startEndSegmnets.Count > 1)
                {
                    for (int x = 0; x < startEndSegmnets.Count - 1; x++)
                    {
                        var startSegment = startEndSegmnets[x];
                        var endSegment = startEndSegmnets[x + 1];

                        if (x % 2 == 0)
                        {
                            CreateTriangles(finalTriangles, startSegment, endSegment);
                        }
                    }
                }
            }

            return finalTriangles;
        }

        //Check if such (or near) point is already present in list
        private static bool IsSimilarTo(double point, List<double> lst)
        {
            for (int i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point, lst[i], Polygon.Epsilon))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSimilarTo(Vector2D point, List<Vector2D> lst)
        {
            for (int i = 0; i < lst.Count; ++i)
            {
                if (MathHelper.WithinEpsilon(point, lst[i], Polygon.Epsilon))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if Y point differnse between aother in list is near <see cref="Polygon.Epsilon"/> value
        /// </summary>
        private static bool IsXPointSimilarTo(Vector2D point, List<Vector2D> lst)
        {
            for (int i = 0; i < lst.Count; ++i)
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
        /// <param name="startSegment"></param>
        /// <param name="endSegment"></param>
        private static void CreateTriangles(List<Vector3F> trianglesList, LineSegment startSegment, LineSegment endSegment)
        {
            if (MathHelper.IsClockwise(startSegment.Start, endSegment.Start, endSegment.End, Vector3D.BackwardRH))
            {
                trianglesList.Add((Vector3F)startSegment.Start);
                trianglesList.Add((Vector3F)endSegment.Start);
                trianglesList.Add((Vector3F)endSegment.End);
            }

            if (MathHelper.IsClockwise(startSegment.Start, endSegment.End, startSegment.End, Vector3D.BackwardRH))
            {
                //Second triangle
                trianglesList.Add((Vector3F)startSegment.Start);
                trianglesList.Add((Vector3F)endSegment.End);
                trianglesList.Add((Vector3F)startSegment.End);
            }
        }

        /// <summary>
        /// Sort points vertically to form correct points sequence for triangulation 
        /// </summary>
        private class VertexHorizontalComparer : IComparer<Vector2D>
        {
            public static VertexHorizontalComparer Defaut => new VertexHorizontalComparer();

            public int Compare(Vector2D x, Vector2D y)
            {
                if (x.X < y.X)
                {
                    return -1;
                }
                else if (MathHelper.WithinEpsilon(x.X, y.X, Polygon.Epsilon))
                {
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
        }

    }
}
