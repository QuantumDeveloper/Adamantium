using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Mathematics
{
    public static class PolygonHelper
    {
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

        public static bool IsSameStartEnd(ref LineSegment2D segment2D)
        {
            return segment2D.Start == segment2D.End;
        }

        /// <summary>
        /// Creates a collection of segments from a collection of points
        /// </summary>
        /// <param name="inPoints"></param>
        /// <param name="isContourClosed"></param>
        /// <returns>Collection of segments if at least 2 points present in collection, otherwise null</returns>
        public static List<LineSegment2D> SplitOnSegments(IEnumerable<Vector2> inPoints, bool isContourClosed = true)
        {
            var points = inPoints as Vector2[] ?? inPoints.ToArray();
            
            if (points.Length <= 1) return new List<LineSegment2D>();
            
            var segments = new List<LineSegment2D>();
            for (var i = 0; i < points.Length - 1; i++)
            {
                var segment = new LineSegment2D(points[i], points[i + 1]);
                if (!IsSameStartEnd(ref segment))
                {
                    segments.Add(segment);
                }
            }

            var seg = new LineSegment2D(points[^1], points[0]);
                
            if (!segments.Contains(seg) && isContourClosed)
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
        /// Creates a collection of segments from a collection of points
        /// </summary>
        /// <param name="inPoints"></param>
        /// <param name="isContourClosed"></param>
        /// <returns>Collection of segments if at least 2 points present in collection, otherwise null</returns>
        public static LineSegment2D[] SplitOnSegments(IEnumerable<Vector3F> inPoints, bool isContourClosed = true)
        {
            var points = inPoints as Vector3F[] ?? inPoints.ToArray();

            if (points.Length <= 1) return Array.Empty<LineSegment2D>();
            
            var segments = new List<LineSegment2D>();
            for (var i = 0; i < points.Length - 1; i++)  
            {
                var segment = new LineSegment2D(points[i], points[i + 1]);
                if (!IsSameStartEnd(ref segment))
                {
                    segments.Add(segment);
                }
            }

            var seg = new LineSegment2D(points[^1], points[0]);
                
            if (!segments.Contains(seg) && isContourClosed)
            {
                //Add last segment
                if (!IsSameStartEnd(ref seg))
                {
                    segments.Add(seg);
                }
            }
            return segments.ToArray();
        }

        /// <summary>
        /// Checks if two points forms single segment in collection of segments
        /// </summary>
        /// <param name="start">Start segment point</param>
        /// <param name="end">End segment point</param>
        /// <param name="segments">Collection of segments</param>
        /// <returns>True is points connected, otherwise false</returns>
        public static bool IsConnected(Vector2 start, Vector2 end, List<LineSegment2D> segments)
        {
            return IsConnectedInvariant(start, end, segments, out var segment2D);
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
        /// This method will also check for such cases and will check for segments very close to each other (less then <see cref="Polygon.Epsilon"/>) 
        /// to 100% find correct segment
        /// </remarks>
        public static bool IsConnectedInvariant(Vector2 start, Vector2 end, List<LineSegment2D> segments, out LineSegment2D correctSegment2D)
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
        
        public static bool IsConnected(Vector2 start, Vector2 end, List<LineSegment2D> segments, out LineSegment2D correctSegment2D)
        {
            correctSegment2D = new LineSegment2D(start, end);
            for (var i = 0; i < segments.Count; ++i)
            {
                var currentSegment = segments[i];
                if (currentSegment == correctSegment2D) continue;
                
                if (currentSegment.Start == end)
                {
                    correctSegment2D = currentSegment;
                    segments.Remove(currentSegment);
                    return true;
                }
                else if (currentSegment.End == end)
                {
                    correctSegment2D = new LineSegment2D(currentSegment.End, currentSegment.Start);
                    segments.Remove(currentSegment);
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
        public static bool GetSegmentsFromPoint(List<LineSegment2D> segments, Vector2 point, out List<LineSegment2D> foundSegments)
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
        public static bool IsPolygonConcave(List<Vector3> points)
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
        public static bool IsPolygonConcave(List<LineSegment2D> segments)
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
        
        public static List<LineSegment2D> OrderSegments(List<LineSegment2D> mergedSegments)
        {
            for (int i = 0; i < mergedSegments.Count; ++i)
            {
                var segment = mergedSegments[i];
                var start = Vector2.Round(segment.Start, 4);
                var end = Vector2.Round(segment.End, 4);
                mergedSegments[i] = new LineSegment2D(start, end);
            }
            
            var currentSegment = mergedSegments[0];
            var orderedSegments = new List<LineSegment2D>();
            orderedSegments.Add(currentSegment);
            var cnt = mergedSegments.Count;
            for (int i = 0; i < cnt; ++i)
            {
                if (IsConnected(currentSegment.Start, currentSegment.End, mergedSegments, out var nextSegment))
                {
                    if (!orderedSegments.Contains(nextSegment))
                    {
                        orderedSegments.Add(nextSegment);
                        currentSegment = nextSegment;
                    }
                }
            }

            return orderedSegments;
        }
    }
}