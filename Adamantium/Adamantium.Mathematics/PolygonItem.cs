using System.Collections.Generic;

namespace Adamantium.Mathematics
{
    /// <summary>
    /// Contains data for one polygon (can contain self intersections)
    /// </summary>
    public class PolygonItem
    {
        /// <summary>
        /// Polygon item name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Collection of points, which describes this <see cref="PolygonItem"/>
        /// </summary>
        public List<Vector2D> Points { get; }

        /// <summary>
        /// Describing does this <see cref="PolygonItem"/> has self intersections
        /// </summary>
        public bool HasSelfIntersections => SelfIntersectedPoints.Count > 0;

        /// <summary>
        /// Defines if this polygon creates a hole in <see cref="Polygon"/>
        /// </summary>
        public bool IsHole { get; internal set; }

        public RectangleF BoundingBox { get; internal set; }

        /// <summary>
        /// Collection of points in places, where it intersects itself
        /// </summary>
        public List<Vector2D> SelfIntersectedPoints { get; }

        /// <summary>
        /// Collection of all segments in certain order describing polygon outline
        /// </summary>
        public List<LineSegment2D> Segments { get; }

        /// <summary>
        /// Collection of segments, which were formed by self intersections
        /// </summary>
        public List<LineSegment2D> SelfIntersectedSegments { get; }

        /// <summary>
        /// Constructs <see cref="PolygonItem"/>
        /// </summary>
        /// <param name="points">polygon points</param>
        public PolygonItem(List<Vector2D> points)
        {
            SelfIntersectedPoints = new List<Vector2D>();
            SelfIntersectedSegments = new List<LineSegment2D>();
            Segments = new List<LineSegment2D>();
            Points = new List<Vector2D>(points);
            CalculateBoundingBox();
        }
        
        public PolygonItem(Vector2D[] points)
        {
            SelfIntersectedPoints = new List<Vector2D>();
            SelfIntersectedSegments = new List<LineSegment2D>();
            Segments = new List<LineSegment2D>();
            Points = new List<Vector2D>(points);
            CalculateBoundingBox();
        }
        
        public PolygonItem(Vector3F[] points)
        {
            var points2D = new Vector2D[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                points2D[i] = (Vector2D) points[i];
            }
            
            SelfIntersectedPoints = new List<Vector2D>();
            SelfIntersectedSegments = new List<LineSegment2D>();
            Segments = new List<LineSegment2D>();
            Points = new List<Vector2D>(points2D);
            CalculateBoundingBox();
        }

        private void CalculateBoundingBox()
        {
            if (Points.Count > 0)
            {
                BoundingBox = RectangleF.FromPoints(Points.ToArray());
            }
        }

        /// <summary>
        /// Check <see cref="PolygonItem"/> for self intersections and fill <see cref="SelfIntersectedPoints"/> and <see cref="SelfIntersectedSegments"/>
        /// </summary>
        /// <param name="rule"></param>
        /// <returns>True if <see cref="PolygonItem"/> has selfintersections, otherwise - false</returns>
        public bool CheckForSelfIntersection(FillRule rule)
        {
            for (var i = 0; i < Segments.Count; ++i)
            {
                for (var j = 0; j < Segments.Count; ++j)
                {
                    var segment1 = Segments[i];
                    var segment2 = Segments[j];
                    if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out var point))
                    {
                        if (!SelfIntersectedPoints.Contains(point) && !Points.Contains(point))
                        {
                            SelfIntersectedPoints.Add(point);
                        }
                    }
                }
            }

            if (HasSelfIntersections)
            {
                UpdatePolygonUsingInterPoints(SelfIntersectedPoints, rule);
                SelfIntersectedSegments.Clear();
                for (var i = 0; i < SelfIntersectedPoints.Count; i++)
                {
                    for (var j = 0; j < SelfIntersectedPoints.Count; j++)
                    {
                        if (!Polygon.IsConnected(SelfIntersectedPoints[i], SelfIntersectedPoints[j], Segments,
                            out var correctSegment2D)) continue;
                        if (!SelfIntersectedSegments.Contains(correctSegment2D))
                        {
                            SelfIntersectedSegments.Add(correctSegment2D);
                        }
                    }
                }
            }
            return HasSelfIntersections;
        }

        /// <summary>
        /// Split <see cref="PolygonItem"/> points on segmnets describing polygone outline
        /// </summary>
        public void SplitOnSegments()
        {
            Segments.Clear();
            var segments = Polygon.SplitOnSegments(Points);
            if (segments != null)
            {
                Segments.AddRange(segments);
            }
        }

        /// <summary>
        /// Update <see cref="PolygonItem"/> points and segments based on intersection points collection and current triangulation rule
        /// </summary>
        /// <param name="intersectionPoints">Collection of intersection points for <see cref="PolygonItem"/> update</param>
        /// <param name="fillRule">Defines current triangulation rule to use (Even-Odd/Non-Zero)</param>
        private void UpdatePolygonUsingInterPoints(List<Vector2D> intersectionPoints, FillRule fillRule)
        {
            if (intersectionPoints.Count <= 0) return;
            var tempSegments = new List<LineSegment2D>(Segments);
            for (var i = 0; i < intersectionPoints.Count; i++)
            {
                var interPoint = intersectionPoints[i];
                if (!Polygon.GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
                for (var j = 0; j < segments.Count; j++)
                {
                    var segment = segments[j];
                    var index = tempSegments.IndexOf(segment);
                    tempSegments.RemoveAt(index);
                    var seg1 = new LineSegment2D(segment.Start, interPoint);
                    //if (Polygon.ContainsInInterPoints(seg1, intersectionPoints, fillRule))
                    {
                        if (Polygon.InsertSegment(tempSegments, seg1, index))
                        {
                            index++;
                        }
                    }
                    var seg2 = new LineSegment2D(interPoint, segment.End);
                    //if (Polygon.ContainsInInterPoints(seg2, intersectionPoints, fillRule))
                    {
                        Polygon.InsertSegment(tempSegments, seg2, index);
                    }
                }
            }
            
            Segments.Clear();
            Segments.AddRange(tempSegments);

            RemoveInternalSegments(fillRule, intersectionPoints);

            UpdatePoints();
        }

        internal void RemoveInternalSegments(FillRule fillRule, List<Vector2D> interPoints)
        {
            //Remove all segments based on intersection points in case of Non-Zero rule
            if (fillRule != FillRule.NonZero || interPoints.Count <= 0) return;
            
            for (var i = 0; i < interPoints.Count; i++)
            {
                for (var j = 0; j < interPoints.Count; j++)
                {
                    var point1 = interPoints[i];
                    var point2 = interPoints[j];
                    if (MathHelper.WithinEpsilon(point1, point2, Polygon.Epsilon))
                    { continue; }

                    if (Polygon.IsConnected(point1, point2, Segments, out var segment2D))
                    {
                        Segments.Remove(segment2D);
                    }
                }
            }
        }

        private void UpdatePoints()
        {
            Points.Clear();
            foreach (var segment in Segments)
            {
                if (!Points.Contains(segment.Start))
                {
                    Points.Add(segment.Start);
                }
                if (!Points.Contains(segment.End))
                {
                    Points.Add(segment.End);
                }
            }
        }

        /// <summary>
        /// Checking if <see cref="PolygonItem"/> is completely contains current <see cref="PolygonItem"/> 
        /// checking each point in <see cref="PolygonItem"/> and raycasting it in 4 directions
        /// </summary>
        /// <param name="polygon"><see cref="PolygonItem"/> to test</param>
        /// <returns>True if all points are completely inside current<see cref="PolygonItem"/> otherwise false</returns>
        public ContainmentType IsCompletelyContains(PolygonItem polygon)
        {
            var counter = 0;
            for (var i = 0; i < polygon.Points.Count; i++)
            {
                var point = polygon.Points[i];
                if (!Polygon.IsPointInsideArea(point, Segments))
                {
                    break;
                }
                counter++;
            }

            if (counter == 0)
            {
                for (var i = 0; i < Segments.Count; i++)
                {
                    for (var j = 0; j < polygon.Segments.Count; j++)
                    {
                        var segment1 = Segments[i];
                        var segment2 = polygon.Segments[j];
                        if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out var point))
                        {
                            counter++;
                            break;
                        }
                    }
                    if (counter > 0)
                    {
                        break;
                    }
                }
            }

            if (counter == 0)
            {
                return ContainmentType.Disjoint;
            }
            
            return counter == polygon.Points.Count ? ContainmentType.Contains : ContainmentType.Intersects;
        }

        /// <summary>
        /// Returns <see cref="PolygonItem"/> name
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Name}";
    }
}
