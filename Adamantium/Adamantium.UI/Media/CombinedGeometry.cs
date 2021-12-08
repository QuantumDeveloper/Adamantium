using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;
using Polygon = Adamantium.Mathematics.Polygon;

namespace Adamantium.UI.Media
{
    public class CombinedGeometry : Geometry
    {
        private Rect bounds;

        public override Rect Bounds => bounds;
        
        public override Geometry Clone()
        {
            throw new System.NotImplementedException();
        }

        public static readonly AdamantiumProperty Geometry1Property =
            AdamantiumProperty.Register(nameof(Geometry1), typeof(Geometry), typeof(CombinedGeometry),
                new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, Geometry1Changed));
        
        public static readonly AdamantiumProperty Geometry2Property =
            AdamantiumProperty.Register(nameof(Geometry2), typeof(Geometry), typeof(CombinedGeometry),
                new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, Geometry2Changed));
        
        public static readonly AdamantiumProperty GeometryCombineModeProperty =
            AdamantiumProperty.Register(nameof(GeometryCombineMode), typeof(GeometryCombineMode), typeof(CombinedGeometry),
                new PropertyMetadata(GeometryCombineMode.Union, PropertyMetadataOptions.AffectsRender, CombineModeChanged));

        private static void Geometry1Changed(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        {
            
        }
        
        private static void Geometry2Changed(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        {
            
        }
        
        private static void CombineModeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        {
            
        }

        public Geometry Geometry1
        {
            get => GetValue<Geometry>(Geometry1Property);
            set => SetValue(Geometry1Property, value);
        }
        
        public Geometry Geometry2
        {
            get => GetValue<Geometry>(Geometry2Property);
            set => SetValue(Geometry2Property, value);
        }
        
        public GeometryCombineMode GeometryCombineMode
        {
            get => GetValue<GeometryCombineMode>(GeometryCombineModeProperty);
            set => SetValue(GeometryCombineModeProperty, value);
        }

        private readonly List<Vector2> selfIntersectedPoints = new List<Vector2>();
        private readonly List<LineSegment2D> mergedSegments = new List<LineSegment2D>();
        private readonly List<Vector2> mergedPoints = new List<Vector2>();
        private readonly HashSet<Vector2> mergedPointsHash = new HashSet<Vector2>();

        public override void RecalculateBounds()
        {
            
        }

        protected internal override void ProcessGeometry()
        {
            selfIntersectedPoints.Clear();
            mergedSegments.Clear();
            mergedPoints.Clear();
            mergedPointsHash.Clear();
            Mesh = new Mesh();
            
            Geometry1.StrokeMesh.SplitOnSegments(Geometry1.IsClosed);
            Geometry2.StrokeMesh.SplitOnSegments(Geometry2.IsClosed);
            
            /*
            var polygon = new Polygon();
            var item1 = new PolygonItem(Geometry1.StrokeMesh.Points);
            var item2 = new PolygonItem(Geometry2.StrokeMesh.Points);
            polygon.AddItems(item1, item2);
            polygon.FillRule = FillRule.NonZero;
            var points = polygon.Fill();
            Mesh.SetPoints(points).Optimize();
            */
            
            if (CheckGeometryIntersections())
            {
                MergePoints();
                MergeSegments();
                
                var edgePoints = new List<Vector2>();
                FindEdgeIntersectionPoints(Geometry1.StrokeMesh, Geometry2.StrokeMesh, out edgePoints);
                
                for (var k = 0; k < edgePoints.Count; ++k)
                {
                    if (!selfIntersectedPoints.Contains(edgePoints[k]))
                    {
                        selfIntersectedPoints.Add(edgePoints[k]);
                    }
                }
                
                UpdatePolygonUsingSelfPoints(edgePoints);
                
                if (GeometryCombineMode == GeometryCombineMode.Union)
                {
                    var intersections = FindAllPossibleIntersections(Geometry1.StrokeMesh, Geometry2.StrokeMesh, edgePoints);
                    RemoveInternalSegments(intersections);
                }
                else if (GeometryCombineMode == GeometryCombineMode.Intersect)
                {
                    var intersections = FindAllPossibleIntersections(Geometry1.StrokeMesh, Geometry2.StrokeMesh, edgePoints);
                    var segments = FindIntersectedSegments(intersections);
                    mergedPoints.Clear();
                    mergedPoints.AddRange(intersections);
                    mergedSegments.Clear();
                    mergedSegments.AddRange(segments);
                }
                else if (GeometryCombineMode == GeometryCombineMode.Exclude)
                {
                    var intersections = FindGeometry1Intersections(Geometry1.StrokeMesh, Geometry2.StrokeMesh, edgePoints);
                    
                    var pointsToRemove = new List<Vector2>();
                    foreach (var meshPoint in Geometry2.StrokeMesh.Points)
                    {
                        pointsToRemove.Add((Vector2)meshPoint);
                    }

                    var intersections2 = FindGeometry1Intersections(Geometry2.StrokeMesh, Geometry1.StrokeMesh,
                        new List<Vector2>());
                    pointsToRemove.AddRange(intersections2);
                    pointsToRemove.AddRange(edgePoints);
                    var diff = intersections.Except(edgePoints).ToArray();
                    foreach (var pt in diff)
                    {
                        pointsToRemove.Remove(pt);
                    }

                    RemoveInternalSegments(pointsToRemove);
                }
            }
            
            

            StrokeMesh = new Mesh();
            StrokeMesh.SetPoints(mergedPoints);
            
            var polygon = new Polygon();
            polygon.FillRule = FillRule.NonZero;
            var points = polygon.FIllDirect(mergedPoints, mergedSegments);
            Mesh.SetPoints(points).Optimize();

            if (GeometryCombineMode != GeometryCombineMode.Xor)
            {
                OrderSegmentsAndPoints();
            }

            // TODO: fix issue with merged segments/points for XOR combine mode
            StrokeMesh = new Mesh();
            StrokeMesh.SetPoints(mergedPoints);

            /*
            mergedPoints.Add(mergedPoints[0]);
            Mesh.SetPoints(mergedPoints);
            Mesh.SetTopology(PrimitiveType.LineStrip);
            */
        }

        private Vector2 RoundVector(Vector2 input)
        {
            input.X = Math.Round(input.X, 4, MidpointRounding.AwayFromZero);
            input.Y = Math.Round(input.Y, 4, MidpointRounding.AwayFromZero);
            return input;
        }

        private void OrderSegmentsAndPoints()
        {
            for (int i = 0; i < mergedSegments.Count; ++i)
            {
                var segment = mergedSegments[i];
                var start = RoundVector(segment.Start);
                var end = RoundVector(segment.End);
                mergedSegments[i] = new LineSegment2D(start, end);
            }
            
            var currentSegment = mergedSegments[0];
            var orderedSegments = new List<LineSegment2D>();
            orderedSegments.Add(currentSegment);
            var cnt = mergedSegments.Count;
            for (int i = 0; i < cnt; ++i)
            {
                if (PolygonHelper.IsConnected(currentSegment.Start, currentSegment.End, mergedSegments, out var nextSegment))
                {
                    if (!orderedSegments.Contains(nextSegment))
                    {
                        orderedSegments.Add(nextSegment);
                        currentSegment = nextSegment;
                    }
                }
            }

            mergedSegments.Clear();
            mergedSegments.AddRange(orderedSegments);
            mergedPoints.Clear();
            foreach (var segment in mergedSegments)
            {
                mergedPoints.Add(segment.Start);
            }
        }

        private void MergePoints()
        {
            mergedPoints.Clear();
            for (int i = 0; i < Geometry1.StrokeMesh.Points.Length; i++)
            {
                var point = (Vector2)Geometry1.StrokeMesh.Points[i];
                mergedPoints.Add(point);
            }
            
            for (int i = 0; i < Geometry2.StrokeMesh.Points.Length; i++)
            {
                var point = (Vector2)Geometry2.StrokeMesh.Points[i];
                mergedPoints.Add(point);
            }
        }
        
        private void MergeSegments()
        {
            mergedSegments.Clear();
            mergedSegments.AddRange(Geometry1.StrokeMesh.Segments);
            mergedSegments.AddRange(Geometry2.StrokeMesh.Segments);
        }
        
        private bool CheckGeometryIntersections()
        {
            var bb1 = Geometry1.Bounds;
            var bb2 = Geometry2.Bounds;
            
            var intersects = bb1.Intersects(bb2);
            
            return intersects;
        }
        
        private bool FindEdgeIntersectionPoints(Mesh mesh1, Mesh mesh2, out List<Vector2> edgePoints)
        {
            edgePoints = new List<Vector2>();
            for (var i = 0; i < mesh1.Segments.Length; i++)
            {
                for (var j = 0; j < mesh2.Segments.Length; j++)
                {
                    var segment1 = mesh1.Segments[i];
                    var segment2 = mesh2.Segments[j];
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
        /// Updates segments for polygons using self Intersection points
        /// </summary>
        /// <param name="selfIntersectionPoints">Self intersection points</param>
        private void UpdatePolygonUsingSelfPoints(List<Vector2> selfIntersectionPoints)
        {
            if (selfIntersectionPoints.Count == 0)
            {
                return;
            }

            var tempSegments = new List<LineSegment2D>(mergedSegments);
            for (var i = 0; i < selfIntersectionPoints.Count; i++)
            {
                var interPoint = selfIntersectionPoints[i];
                if (!PolygonHelper.GetSegmentsFromPoint(tempSegments, interPoint, out var segments)) continue;
                
                for (var j = 0; j < segments.Count; j++)
                {
                    var segment = segments[j];
                    
                    if (MathHelper.WithinEpsilon(interPoint, segment.Start, Polygon.Epsilon) ||
                        MathHelper.WithinEpsilon(interPoint, segment.End, Polygon.Epsilon))
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
            mergedSegments.Clear();
            mergedSegments.AddRange(tempSegments);

            UpdateMergedPoints();
        }
        
        /// <summary>
        /// Remove all segments based on intersection points in case of Non-Zero rule
        /// </summary>
        /// <param name="interPoints"></param>
        private void RemoveInternalSegments(List<Vector2> interPoints)
        {
            for (var i = 0; i < interPoints.Count; i++)
            {
                for (var j = 0; j < interPoints.Count; j++)
                {
                    var point1 = interPoints[i];
                    var point2 = interPoints[j];
                    if (MathHelper.WithinEpsilon(point1, point2, Polygon.Epsilon))
                    { continue; }

                    if (PolygonHelper.IsConnectedInvariant(point1, point2, mergedSegments, out var segment2D))
                    {
                        mergedSegments.Remove(segment2D);
                    }
                }
            }
            UpdateMergedPoints();
        }
        
        private List<LineSegment2D> FindIntersectedSegments(List<Vector2> interPoints)
        {
            var segments = new List<LineSegment2D>();
            for (var i = 0; i < interPoints.Count; i++)
            {
                for (var j = 0; j < interPoints.Count; j++)
                {
                    var point1 = interPoints[i];
                    var point2 = interPoints[j];
                    if (MathHelper.WithinEpsilon(point1, point2, Polygon.Epsilon))
                    { continue; }

                    if (PolygonHelper.IsConnectedInvariant(point1, point2, mergedSegments, out var segment2D))
                    {
                        mergedSegments.Remove(segment2D);
                        segments.Add(segment2D);
                    }
                }
            }
            //UpdateMergedPoints();
            return segments;
        }
        
        /// <summary>
        /// Find all possible intersections between 2 polygons including intersection points and remove all merged segments 
        /// if any of 2 points are connected as segments. This needs for Non-zero rule to remove all inner segments between 2 polygons to correctly triangulate them
        /// </summary>
        /// <param name="mesh1">first polygon</param>
        /// <param name="mesh2">second polygon</param>
        /// <param name="edgePoints">intersection points between 2 polygons, which were found earlier on previous step</param>
        /// <returns>Collection of intersection points, including points, which is not lying on segments, but also inside polygons.</returns>
        /// <remarks>During running this method, it will remove all merged segments from <see cref="Polygon"/> if some of its segments contains both intersection points</remarks>
        private List<Vector2> FindAllPossibleIntersections(Mesh mesh1, Mesh mesh2, List<Vector2> edgePoints)
        {
            var allIntersections = new List<Vector2>(edgePoints);

            for (var i = 0; i < mesh1.Points.Length; i++)
            {
                var point = (Vector2)mesh1.Points[i];
                if (Collision2D.IsPointInsideArea(point, mesh2.Segments) &&
                    !allIntersections.Contains(point))
                {
                    allIntersections.Add(point);
                }
            }
            for (var i = 0; i < mesh2.Points.Length; i++)
            {
                var point = (Vector2)mesh2.Points[i];
                if (Collision2D.IsPointInsideArea(point, mesh1.Segments) &&
                    !allIntersections.Contains(point))
                {
                    allIntersections.Add(point);
                }
            }

            return allIntersections;
        }
        
        private List<Vector2> FindGeometry1Intersections(Mesh mesh1, Mesh mesh2, List<Vector2> edgePoints)
        {
            var allIntersections = new List<Vector2>(edgePoints);

            // for (var i = 0; i < mesh1.Points.Length; i++)
            // {
            //     var point = (Vector2)mesh1.Points[i];
            //     if (Collision2D.IsPointInsideArea(point, mesh2.Segments) &&
            //         !allIntersections.Contains(point))
            //     {
            //         allIntersections.Add(point);
            //     }
            // }
            for (var i = 0; i < mesh2.Points.Length; i++)
            {
                var point = (Vector2)mesh2.Points[i];
                if (Collision2D.IsPointInsideArea(point, mesh1.Segments) &&
                    !allIntersections.Contains(point))
                {
                    allIntersections.Add(point);
                }
            }

            return allIntersections;
        }
        
       
        
        private void UpdateMergedPoints()
        {
            mergedPoints.Clear();
            mergedPointsHash.Clear();
            for (var i = 0; i < mergedSegments.Count; ++i)
            {
                var segment = mergedSegments[i];
                if (!mergedPointsHash.Contains(segment.Start))
                {
                    mergedPoints.Add(segment.Start);
                    mergedPointsHash.Add(segment.Start);
                }

                if (!mergedPointsHash.Contains(segment.End))
                {
                    mergedPoints.Add(segment.End);
                    mergedPointsHash.Add(segment.End);
                }
            }
        }
    }
}