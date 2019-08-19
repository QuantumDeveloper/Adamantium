using System.Collections.Generic;

namespace Adamantium.Mathematics
{
    /// <summary>
    /// Contains data for one polygon (can contain self intersections)
    /// </summary>
    public class PolygonItem
    {
        /// <summary>
        /// Po;ygon item name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Collection of points, which describes this <see cref="PolygonItem"/>
        /// </summary>
        public List<Vector3D> Points { get; }

        /// <summary>
        /// Describing does this <see cref="PolygonItem"/> has selfintersections
        /// </summary>
        public bool HasSelfIntersections => SelfIntersectedPoints.Count > 0;

        /// <summary>
        /// Defines if this polygon creates a hole in <see cref="Polygon"/>
        /// </summary>
        public bool IsHole { get; internal set; }

        public OrientedBoundingBox BoundingBox { get; internal set; }

        /// <summary>
        /// Collection of points in places, where it intersects itself
        /// </summary>
        public List<Vector3D> SelfIntersectedPoints { get; }

        /// <summary>
        /// Collection of all segments in certain order describing polygon outline
        /// </summary>
        public List<LineSegment> Segments { get; }

        /// <summary>
        /// Collection of segments, which were formed by self intersections
        /// </summary>
        public List<LineSegment> SelfIntersectedSegments { get; }

        /// <summary>
        /// Defines width and height of polygon
        /// </summary>
        public Vector2F Size { get; private set; }

        /// <summary>
        /// Constructs <see cref="PolygonItem"/>
        /// </summary>
        /// <param name="points">polygon points</param>
        public PolygonItem(List<Vector3D> points)
        {
            SelfIntersectedPoints = new List<Vector3D>();
            SelfIntersectedSegments = new List<LineSegment>();
            Segments = new List<LineSegment>();
            Points = new List<Vector3D>(points);
            CalculateBoundingBox();
        }

        /// <summary>
        /// Constructs <see cref="PolygonItem"/>
        /// </summary>
        /// <param name="points">polygon points</param>
        public PolygonItem(List<Vector3F> points)
        {
            SelfIntersectedPoints = new List<Vector3D>();
            SelfIntersectedSegments = new List<LineSegment>();
            Segments = new List<LineSegment>();
            Points = new List<Vector3D>();
            for (int i = 0; i < points.Count; i++)
            {
                Points.Add(points[i]);
            }
            CalculateBoundingBox();
        }

        private void CalculateBoundingBox()
        {
            if (Points.Count > 0)
            {
                BoundingBox = OrientedBoundingBox.FromPoints(Points.ToArray());
                //Size = new Size2F(BoundingBox.Size.X, BoundingBox.Size.Y);
            }
        }

        /// <summary>
        /// Check <see cref="PolygonItem"/> for self intersections and fill <see cref="SelfIntersectedPoints"/> and <see cref="SelfIntersectedSegments"/>
        /// </summary>
        /// <param name="rule"></param>
        /// <returns>True if <see cref="PolygonItem"/> has selfintersections, otherwise - false</returns>
        public bool CheckForSelfIntersection(FillRule rule)
        {
            for (int i = 0; i < Segments.Count; ++i)
            {
                for (int j = 0; j < Segments.Count; ++j)
                {
                    Vector3D point;
                    var segment1 = Segments[i];
                    var segment2 = Segments[j];
                    if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out point))
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
                for (int i = 0; i < SelfIntersectedPoints.Count; i++)
                {
                    for (int j = 0; j < SelfIntersectedPoints.Count; j++)
                    {
                        LineSegment correctSegment;
                        if (Polygon.IsConnected(SelfIntersectedPoints[i], SelfIntersectedPoints[j], Segments, out correctSegment))
                        {
                            if (!SelfIntersectedSegments.Contains(correctSegment))
                            {
                                SelfIntersectedSegments.Add(correctSegment);
                            }
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
        public void UpdatePolygonUsingInterPoints(List<Vector3D> intersectionPoints, FillRule fillRule)
        {
            if (intersectionPoints.Count > 0)
            {
                List<LineSegment> tempSegments = new List<LineSegment>(Segments);
                for (int i = 0; i < intersectionPoints.Count; i++)
                {
                    var interPoint = intersectionPoints[i];
                    List<LineSegment> segments;
                    if (Polygon.GetSegmentsFromPoint(tempSegments, interPoint, out segments))
                    {
                        for (int j = 0; j < segments.Count; j++)
                        {
                            var segment = segments[j];
                            var index = tempSegments.IndexOf(segment);
                            tempSegments.RemoveAt(index);
                            LineSegment seg1 = new LineSegment(segment.Start, interPoint);
                            //if (Polygon.ContainsInInterPoints(seg1, intersectionPoints, fillRule))
                            {
                                if (Polygon.InsertSegment(tempSegments, seg1, index))
                                {
                                    index++;
                                }
                            }
                            LineSegment seg2 = new LineSegment(interPoint, segment.End);
                            //if (Polygon.ContainsInInterPoints(seg2, intersectionPoints, fillRule))
                            {
                                Polygon.InsertSegment(tempSegments, seg2, index);
                            }
                        }
                    }
                }
                Segments.Clear();
                Segments.AddRange(tempSegments);

                RemoveInternalSegments(fillRule, intersectionPoints);

                UpdatePoints();
            }
        }

        internal void RemoveInternalSegments(FillRule fillRule, List<Vector3D> interPoints)
        {
            //Remove all segments based on intersection points in case of Non-Zero rule
            if (fillRule == FillRule.NonZero && interPoints.Count > 0)
            {
                for (int i = 0; i < interPoints.Count; i++)
                {
                    for (int j = 0; j < interPoints.Count; j++)
                    {
                        var point1 = interPoints[i];
                        var point2 = interPoints[j];
                        if (MathHelper.WithinEpsilon(point1, point2, Polygon.Epsilon))
                        { continue; }

                        LineSegment segment;
                        if (Polygon.IsConnected(interPoints[i], interPoints[j], Segments, out segment))
                        {
                            Segments.Remove(segment);
                        }
                    }
                }
            }
        }

        private void UpdatePoints()
        {
            Points.Clear();
            foreach (LineSegment segment in Segments)
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
            int counter = 0;
            for (int i = 0; i < polygon.Points.Count; i++)
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
                for (int i = 0; i < Segments.Count; i++)
                {
                    for (int j = 0; j < polygon.Segments.Count; j++)
                    {
                        var segment1 = Segments[i];
                        var segment2 = polygon.Segments[j];
                        Vector3D point;
                        if (Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out point))
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
            if (counter == polygon.Points.Count)
            {
                return ContainmentType.Contains;
            }

            return ContainmentType.Intersects;
        }

        /// <summary>
        /// Returns <see cref="PolygonItem"/> name
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{Name}";
    }
}
