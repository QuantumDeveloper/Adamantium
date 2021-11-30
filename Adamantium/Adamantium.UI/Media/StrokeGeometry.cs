using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class StrokeGeometry : Geometry
   {
      private Rect bounds;
      private int lastIndex = 0;
      private const int interrupt = -1;
      private int doublePrecision = 4;
      internal uint BezierSampleRate { get; set; } = 15;
      internal double ArcSampleRate { get; set; } = 0.1;

      public StrokeGeometry()
      {
      }

      public StrokeGeometry(Pen pen, Geometry geometry)
      {
         GenerateStroke(pen, geometry);
      }

      private void GenerateStroke(Pen pen, Geometry geometry)
      {
         if (geometry.StrokeMesh.Positions.Length == 0) return;
         
         pen.PenLineJoin = PenLineJoin.Bevel;
         //@TODO check concave zero length triangulation
         pen.StartLineCap = PenLineCap.Flat;
         pen.EndLineCap = PenLineCap.Flat;
         var points = geometry.StrokeMesh.Positions;

         // for test purposes start
         geometry.IsClosed = false;
         var testSegs = new List<Vector3F>();

         /*testSegs.Add(new Vector3F(100, 100, 0));
         testSegs.Add(new Vector3F(200, 100, 0));
         testSegs.Add(new Vector3F(200, 200, 0));
         testSegs.Add(new Vector3F(100, 200, 0));*/

         testSegs.Add(new Vector3F(100, 100, 0));
         testSegs.Add(new Vector3F(200, 100, 0));

         points = testSegs.ToArray();
         var zeroLineDir = new LineSegment2D(new Vector2D(0, 0), new Vector2D(0.3, 1)).DirectionNormalized;
         // for test purposes end

         // sanitize geometry - remove equal points
         var index = 0;
         bool goOn = true;
         var pointList = points.ToList();
         do
         {
            var nextIndex = index + 1;

            if (index == pointList.Count - 1)
            {
               nextIndex = 0;
               goOn = false;
            }

            if (pointList[index] == pointList[nextIndex]) pointList.RemoveAt(nextIndex);
            else index++;
         } while (goOn);

         /*points = pointList.ToArray();

         List<Vector3F> vertices;
         
         if (pen.DashStrokeArray == null || pen.DashStrokeArray.Count == 0)
         {
            vertices = GenerateStroke(points, pen, geometry.IsClosed, zeroLineDir);
         }
         else
         {
            vertices = GenerateDashes(points, pen, 10, geometry.IsClosed);
         }

         if (vertices != null && vertices.Count > 0) Mesh.SetPositions(vertices);*/

         // var shape1 = new List<LineSegment2D>
         // {
         //    new (new Vector2D(100, 100), new Vector2D(200, 100)),
         //    new (new Vector2D(200, 100), new Vector2D(200, 200)),
         //    new (new Vector2D(200, 200), new Vector2D(100, 200)),
         //    new (new Vector2D(100, 200), new Vector2D(100, 100))
         // };
         //
         // var shape2 = new List<LineSegment2D>
         // {
         //    new (new Vector2D(150, 100), new Vector2D(250, 100)),
         //    new (new Vector2D(250, 100), new Vector2D(250, 200)),
         //    new (new Vector2D(250, 200), new Vector2D(150, 200)),
         //    new (new Vector2D(150, 200), new Vector2D(150, 100))
         // };

         var shape1 = new List<Vector2D>
         {
            new (100, 100),
            new (200, 100),
            new (200, 200),
            new (100, 200)
         };
         
         var shape2 = new List<Vector2D>
         {
            new (150, 100),
            new (250, 100),
            new (250, 200),
            new (150, 200)
         };
         
         var polygon = new Polygon();
         var polItem1 = new PolygonItem(shape1);
         var polItem2 = new PolygonItem(shape2);
         
         polygon.Polygons.Add(polItem1);
         polygon.Polygons.Add(polItem2);

         polygon.FillRule = FillRule.NonZero;
         var res = polygon.Fill();
         
         
         
         testSegs.Clear();
         
         /*foreach (var segment in shape1)
         {
            testSegs.Add(new Vector3F((float)segment.Start.X, (float)segment.Start.Y, 0));
            testSegs.Add(new Vector3F((float)segment.End.X, (float)segment.End.Y, 0));
         }

         foreach (var segment in shape2)
         {
            testSegs.Add(new Vector3F((float)segment.Start.X, (float)segment.Start.Y, 0));
            testSegs.Add(new Vector3F((float)segment.End.X, (float)segment.End.Y, 0));
         }*/

         //var mergedShape = MergeShapes(shape1, shape2);
         /*var mergedShape = new List<LineSegment2D>();
         
         mergedShape.AddRange(shape1);
         mergedShape.AddRange(shape2);*/
         
         /*foreach (var segment in mergedShape)
         {
            testSegs.Add(new Vector3F((float)segment.Start.X, (float)segment.Start.Y, 0));
            testSegs.Add(new Vector3F((float)segment.End.X, (float)segment.End.Y, 0));
         }*/
         
         /*var geometrySegments = new List<LineSegment2D>
         {
            new (new Vector2D(100, 100), new Vector2D(200, 100)),
            new (new Vector2D(200, 100), new Vector2D(300, 200))
         };

         var mergedShape = new List<LineSegment2D>();
         
         foreach (var geometrySegment in geometrySegments)
         {
            var strokeCore = GenerateStrokeCore(geometrySegment, pen.Thickness);

            mergedShape = MergeShapes(strokeCore, mergedShape);
         }*/
         
         // foreach (var segment in mergedShape)
         // {
         //    testSegs.Add(new Vector3F((float)segment.Start.X, (float)segment.Start.Y, 0));
         //    testSegs.Add(new Vector3F((float)segment.End.X, (float)segment.End.Y, 0));
         // }

         points = res.ToArray();
         
         Mesh.SetPositions(points);
         Mesh.SetTopology(PrimitiveType.TriangleList);
         Mesh.GenerateBasicIndices();
      }

      private List<LineSegment2D> MergeShapes(List<LineSegment2D> shape1, List<LineSegment2D> shape2)
      {
         var mergedShape = new List<LineSegment2D>();

         mergedShape.AddRange(SubtractShape(shape1, shape2));
         mergedShape.AddRange(SubtractShape(shape2, mergedShape));
         
         return mergedShape;
      }

      private List<LineSegment2D> SubtractShape(List<LineSegment2D> shape, List<LineSegment2D> stencil)
      {
         if (stencil == null || stencil.Count == 0) return shape;
         
         var subtractedSegments = new List<LineSegment2D>();
         
         bool isInsideStencil = false;
         
         foreach (var segment in shape)
         {
            //@TODO can be optimized if segments are consequent, just check once if we are starting inside or outside stencil, and then just toggle this flag at each intersection
            isInsideStencil = MathHelper.IsPointInShape(segment.Start, stencil); 
            var intersections = GetSegmentIntersectionsWithShape(segment, stencil);

            for (var i = 0; i <= intersections.Count; i++, isInsideStencil = !isInsideStencil)
            {
               var start = (i == 0) ? segment.Start : (Vector2D)intersections.GetByIndex(i - 1);
               var end = (i == intersections.Count) ? segment.End : (Vector2D)intersections.GetByIndex(i);
               
               if (!isInsideStencil && start != end) subtractedSegments.Add(new LineSegment2D(start, end));
            }
         }

         return subtractedSegments;
      }

      private SortedList GetSegmentIntersectionsWithShape(LineSegment2D segment, List<LineSegment2D> shape)
      {
         var sortedIntersections = new SortedList();

         foreach (var shapeSegment in shape)
         {
            var shapeSeg = shapeSegment;

            if (!Collision2D.SegmentSegmentIntersection(ref segment, ref shapeSeg, out var intersection)) continue;
            
            var length = (intersection - segment.Start).Length();
            sortedIntersections.Add(length, intersection);
         }

         return sortedIntersections;
      }

      private List<LineSegment2D> GenerateStrokeCore(LineSegment2D geometrySegment, double thickness)
      {
         var topBottomSegments = GenerateTopBottomSegments(geometrySegment.Start, geometrySegment.End, thickness);

         var strokeCore = new List<LineSegment2D>
         {
            topBottomSegments[0],
            new (topBottomSegments[0].End, topBottomSegments[1].End),
            new (topBottomSegments[1].End, topBottomSegments[1].Start),
            new (topBottomSegments[1].Start, topBottomSegments[0].Start)
         };

         return strokeCore;
      }
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      
      private List<Vector3F> GenerateDashes(Vector3F[] points, Pen pen, double offset, bool isGeometryClosed)
      {
         var vertices = new List<Vector3F>();
         var dashesGeometry = SplitGeometryToDashes(points, pen, offset, isGeometryClosed);

         foreach (var dashGeometry in dashesGeometry)
         {
            var triangulatedDash = GenerateStroke(dashGeometry.ToArray(), pen, false);
            
            if (triangulatedDash != null) vertices.AddRange(triangulatedDash);
         }

         return vertices;
      }
      
      private double GetGeometryLength(Vector3F[] points, bool isGeometryClosed)
      {
         var length = 0.0;

         for (var i = 0; i < points.Length; i++)
         {
            var next = i + 1;

            if (i == points.Length - 1)
            {
               if (isGeometryClosed) next = 0;
               else break;
            }

            var start = (Vector2D)points[i];
            var end = (Vector2D)points[next];

            length += (end - start).Length();
         }

         return length;
      }
      
      private List<List<Vector3F>> SplitGeometryToDashes(Vector3F[] points, Pen pen, double offset, bool isGeometryClosed)
      {
         var dashesGeometry = new List<List<Vector3F>>(); 
         
         var remainingGeometryLength = GetGeometryLength(points, isGeometryClosed);

         // clip the offset if it is too long and is greater then geometry length
         offset %= remainingGeometryLength;

         var dashArrayIndex = 0;
         var dashArrayCounter = 0;
         var dashGeometry = new List<Vector3F>();
         bool goOn = true;

         var currentPoint = GetPointAlongGeometry(points, 0, (Vector2D)points[0], offset, isGeometryClosed, out var currentSegmentIndex, out var intermediatePointIndices, out var pathLength);

         do
         {
            // loop dash array usage
            if (dashArrayIndex == pen.DashStrokeArray.Count) dashArrayIndex = 0;
            
            // determine if this is dash or space, if dash - add first point of dash
            if (dashArrayCounter % 2 == 0) dashGeometry.Add((Vector3F)currentPoint);

            var currentOffset = pen.DashStrokeArray[dashArrayIndex];
            
            // move offset in accordance to line cap endings
            if (dashArrayCounter % 2 != 0)
            {
               // if not flat, add prev dash's end cap width
               if (pen.EndLineCap != PenLineCap.Flat) currentOffset += pen.Thickness / 2.0;
               
               // if not flat, add next dash's start cap width
               if (pen.StartLineCap != PenLineCap.Flat) currentOffset += pen.Thickness / 2.0;
            }

            if (currentOffset >= remainingGeometryLength)
            {
               goOn = false;
               currentOffset = remainingGeometryLength;
            }

            currentPoint = GetPointAlongGeometry(points, currentSegmentIndex, currentPoint, currentOffset, isGeometryClosed, out currentSegmentIndex, out intermediatePointIndices, out pathLength);

            if (dashArrayCounter % 2 == 0)
            {
               if (isGeometryClosed)
               {
                  dashGeometry.AddRange(intermediatePointIndices.Select(index => points[index]));
                  dashGeometry.Add((Vector3F)currentPoint);
                  dashesGeometry.Add(dashGeometry);
                  dashGeometry = new List<Vector3F>();
               }
               else
               {
                  foreach (var pointIndex in intermediatePointIndices)
                  {
                     dashGeometry.Add(points[pointIndex]);
                     
                     if (pointIndex == points.Length - 1)
                     {
                        dashesGeometry.Add(dashGeometry);
                        dashGeometry = new List<Vector3F>();
                        dashGeometry.Add(points[0]);
                     }
                  }
                  
                  dashGeometry.Add((Vector3F)currentPoint);
                  dashesGeometry.Add(dashGeometry);
                  dashGeometry = new List<Vector3F>();
               }
            }

            if (!goOn) break;
            
            remainingGeometryLength -= pathLength;
            dashArrayIndex++;
            dashArrayCounter++;
         } while (true);

         // merge last and first dashes only if last dash is not empty space and it's end equals to first dash's start
         if (dashesGeometry[^1][^1] == dashesGeometry[0][0])
         {
            var mergedDash = dashesGeometry.Last();
            
            // remove last point of last dash - it is the same as first point of first dash and will be added along with other first dash points
            mergedDash.RemoveAt(mergedDash.Count - 1);
            mergedDash.AddRange(dashesGeometry[0]);
            
            // rewrite original first dash
            dashesGeometry[0] = mergedDash;
            
            // remove original last dash
            dashesGeometry.RemoveAt(dashesGeometry.Count - 1);
         }
         
         return dashesGeometry;
      }

      private Vector2D GetPointAlongGeometry(Vector3F[] points, int startSegmentIndex, Vector2D startPoint, double offsetFromStartPoint, bool isGeometryClosed, out int segmentIndex, out List<int> intermediatePointIndices, out double pathLength)
      {
         segmentIndex = startSegmentIndex;
         intermediatePointIndices = new List<int>();
         pathLength = 0.0;
         
         // if offset is 0 - return start point of geometry
         if (offsetFromStartPoint == 0) return startPoint;
         
         // determine the direction of movement along geometry - positive offset is forward, negative offset is backwards
         var moveForward = offsetFromStartPoint > 0;
         offsetFromStartPoint = Math.Abs(offsetFromStartPoint);

         // index of start point of segment on which lies the point with desired offset
         var index = startSegmentIndex;
         Vector2D segmentStart;
         Vector2D segmentEnd;

         bool useStartPoint = true;
         
         // determine on which segment lies the point with desired offset
         do
         {
            // loop the cycle according to geometry type and move direction
            if (isGeometryClosed)
            {
               if (moveForward)
               {
                  if (index >= points.Length) index = 0;
               }
               else
               {
                  if (index <= -1) index = points.Length - 1;
               }
            }
            else
            {
               if (moveForward)
               {
                  if (index >= points.Length - 1) index = 0;
               }
               else
               {
                  if (index <= 0) index = points.Length - 1;
               }
            }

            var nextIndex = index + (moveForward ? 1 : -1);

            if (nextIndex == points.Length) nextIndex = 0;
            if (nextIndex == -1) nextIndex = points.Length - 1;

            segmentStart = useStartPoint ? startPoint : (Vector2D)points[index];
            segmentEnd = (Vector2D)points[nextIndex];

            // use start point only at the start of new 'path', so use it only once
            if (useStartPoint) useStartPoint = false;

            var currentSegmentLength = (segmentEnd - segmentStart).Length();
            var currentOffsetDiff = offsetFromStartPoint - currentSegmentLength;

            // offset point is directly in the end of current segment, return the end point
            if (currentOffsetDiff == 0)
            {
               segmentIndex = nextIndex;
               pathLength += currentSegmentLength;
               return segmentEnd;
            }

            // we found the segment on which required point lies, break the cycle
            if (currentOffsetDiff < 0)
            {
               if (!moveForward) segmentIndex = nextIndex;
               pathLength += offsetFromStartPoint;
               break;
            }

            offsetFromStartPoint = currentOffsetDiff;
            segmentIndex = nextIndex;
            intermediatePointIndices.Add(nextIndex);
            pathLength += currentSegmentLength;
            index += (moveForward ? 1 : -1);
         } while (true);

         return GetPointAlongSegment(segmentStart, segmentEnd, offsetFromStartPoint);
      }

      private Vector2D GetPointAlongSegment(Vector2D start, Vector2D end, double offsetFromStartOfSegment)
      {
         var segment = new LineSegment2D(start, end);

         var direction = segment.DirectionNormalized;

         return start + offsetFromStartOfSegment * direction;
      }

      private void GenerateDashStroke(Vector3F[] points, Pen pen, bool isGeometryClosed)
      {
         var dashArray = pen.DashStrokeArray;
         
         var topSegments = new List<LineSegment2D>();
         var bottomSegments = new List<LineSegment2D>();
         
         var topSegmentsArray = new List<List<LineSegment2D>>();
         var bottomSegmentsArray = new List<List<LineSegment2D>>();

         var dashPointsArray = new List<List<Vector3F>>();

         var dashPoints = new List<Vector3F>();

         Vector2D currentPoint = (Vector2D)points[0];
         int dashIndex = 0;
         double dashLength = 0;

         // Drawing dashes
         for (int i = 0; i < points.Length;)
         {
            var nextIndex = i + 1;

            if (i == points.Length - 1)
            {
               //if (!isGeometryClosed) break;
              // nextIndex = 0;
            }

            var dash = dashArray[dashIndex % 2];
            var nextPoint = (Vector2D)points[nextIndex];
            var dir = nextPoint - currentPoint;

            bool isSpace = dashIndex % 2 == 1;

            var length = dir.Length();
            dir.Normalize();
            if (length < dash)
            {
               dashLength = dash - length;
               dash = length;
            }
            else
            {
               dashIndex++;
               if (!isSpace)
               {
                  bottomSegments = new List<LineSegment2D>();
                  topSegments = new List<LineSegment2D>();
                  dashPoints = new List<Vector3F>();
               }
            }

            if (dashLength > 0)
            {
               //dash = dashLength;
            }

            var nextDashPoint = currentPoint + dash * dir;

            if (!isSpace)
            {
               var topBottomLines = GenerateTopBottomSegments(currentPoint, nextDashPoint, pen.Thickness);
               
               dashPoints.Add((Vector3F)currentPoint);
               dashPoints.Add((Vector3F)nextDashPoint);
               
               dashPointsArray.Add(dashPoints);

               bottomSegments.Add(topBottomLines[1]);
               topSegments.Add(topBottomLines[0]);

               bottomSegmentsArray.Add(bottomSegments);
               topSegmentsArray.Add(topSegments);
            }

            currentPoint = nextDashPoint;

            if (currentPoint == nextPoint)
            {
               i++;
               if (i >= points.Length -1) break;
            }
         }

         var vertices = new List<Vector3F>();
         for (int x = 0; x < topSegmentsArray.Count; ++x)
         {
            var sampledTopPart = new List<Vector2D>();
            var sampledBottomPart = new List<Vector2D>();
            var strokeOutline = new List<Vector2D>();

            dashPoints = dashPointsArray[x];

            var currentTopSegments = topSegmentsArray[x];
            var currentBottomSegments = bottomSegmentsArray[x];

            sampledTopPart.Add(currentTopSegments[0].Start);
            sampledBottomPart.Add(currentBottomSegments[0].Start);

            for (int i = 0; i < currentTopSegments.Count; i++)
            {
               var nextIndex = i + 1;

               if (i == currentTopSegments.Count - 1)
               {
                  if (!isGeometryClosed) break;
                  nextIndex = 0;
               }

               /*GenerateStrokeJoin(currentTopSegments[i], currentTopSegments[nextIndex], sampledTopPart, pen.PenLineJoin);
               GenerateStrokeJoin(currentBottomSegments[i], currentBottomSegments[nextIndex], sampledBottomPart, pen.PenLineJoin);*/
            }

            sampledTopPart.Add(currentTopSegments[^1].End);
            sampledBottomPart.Add(currentBottomSegments[^1].End);

            strokeOutline.AddRange(sampledTopPart);
            sampledBottomPart.Reverse();
            strokeOutline.AddRange(sampledBottomPart);

            RemoveCollinearSegments(strokeOutline, isGeometryClosed);

            // ear clipping triangulation ( https://www.youtube.com/watch?v=QAdfkylpYwc )
            var strokePoints = TriangulateSimplePolygon(strokeOutline);

            if (strokePoints != null)
            {
               vertices.AddRange(strokePoints);
            }

            /*var capPoints = GenerateLineCaps(pen, dashPoints.ToArray());
            if (capPoints != null)
            {
               vertices.AddRange(capPoints);
            }*/
         }

         Mesh.SetPositions(vertices).Optimize();
      }

      /// <summary>
      /// Generates triangulated stroke and puts it into Mesh for render
      /// </summary>
      /// <param name="points">Geometry points</param>
      /// <param name="pen">Stroke's pen</param>
      /// <param name="isGeometryClosed">Is geometry closed (like triangle, square etc.) or is it just a line</param>
      /// <param name="zeroLengthLineDirection">Direction for the case of zero length line</param>
      private List<Vector3F> GenerateStroke(Vector3F[] points, Pen pen, bool isGeometryClosed, Vector2D? zeroLengthLineDirection = null)
      {
         // check if geometry is valid
         if (points.Length < 2) return null;

         var topSegments = new List<LineSegment2D>();
         var bottomSegments = new List<LineSegment2D>();
         
         // corner case - if line has zero length
         if (points.Length == 2 && points[0] == points[1])
         {
            // if there is no direction for zero length line - set it to default value of (1,0)
            if (zeroLengthLineDirection == null) zeroLengthLineDirection = new Vector2D(1, 0);

            // compute normal to zero length line based on zero length line direction
            var zeroLengthLineNormal = new Vector2D(-zeroLengthLineDirection.Value.Y, zeroLengthLineDirection.Value.X);
            
            // generate top and bottom segments
            var startTopBottomPoints = GenerateTopBottomPoints((Vector2D)points[0], pen.Thickness, zeroLengthLineNormal);
            var endTopBottomPoints = GenerateTopBottomPoints((Vector2D)points[1], pen.Thickness, zeroLengthLineNormal);

            topSegments.Add(new LineSegment2D(startTopBottomPoints[0], endTopBottomPoints[0]));
            bottomSegments.Add(new LineSegment2D(startTopBottomPoints[1], endTopBottomPoints[1]));
         }
         else
         {
            // ensure that zero length line direction will not be used further
            zeroLengthLineDirection = null;
            
            // generate top and bottom parts of stroke outline
            for (var i = 0; i < points.Length; ++i)
            {
               var nextIndex = i + 1;

               if (i == points.Length - 1)
               {
                  if (!isGeometryClosed) break;

                  // loop cycle around
                  nextIndex = 0;
               }

               var topBottomSegments =
                  GenerateTopBottomSegments((Vector2D) points[i], (Vector2D) points[nextIndex], pen.Thickness);

               topSegments.Add(topBottomSegments[0]);
               bottomSegments.Add(topBottomSegments[1]);
            }
         }

         var sampledStrokeTopOutlinePart = new List<Vector2D>();
         var sampledStrokeBottomOutlinePart = new List<Vector2D>();

         // add start points, in cycle we will be adding only end points
         sampledStrokeTopOutlinePart.Add(topSegments[0].Start);
         sampledStrokeBottomOutlinePart.Add(bottomSegments[0].Start);

         // we only need to calculate joins if points count greater than 2
         if (points.Length > 2)
         {
            for (var i = 0; i < topSegments.Count; i++)
            {
               var nextIndex = i + 1;

               if (i == topSegments.Count - 1)
               {
                  if (!isGeometryClosed) break;
                  nextIndex = 0;
               }

               // generate join points for top and bottom outline parts separately
               GenerateStrokeJoin(topSegments[i], topSegments[nextIndex], sampledStrokeTopOutlinePart, true, pen.PenLineJoin);
               GenerateStrokeJoin(bottomSegments[i], bottomSegments[nextIndex], sampledStrokeBottomOutlinePart, false, pen.PenLineJoin);
            }
         }

         if (!isGeometryClosed)
         {
            // add last pair of end points
            sampledStrokeTopOutlinePart.Add(topSegments[^1].End);
            sampledStrokeBottomOutlinePart.Add(bottomSegments[^1].End);
         }
         else
         {
            // to prevent floating precision issues
            sampledStrokeTopOutlinePart[0] = sampledStrokeTopOutlinePart[^1];
            sampledStrokeBottomOutlinePart[0] = sampledStrokeBottomOutlinePart[^1];
         }

         // compose stroke outline from top / bottom parts and caps outlines
         var strokeOutline = ComposeStrokeOutline(pen, points, sampledStrokeTopOutlinePart, sampledStrokeBottomOutlinePart, isGeometryClosed, zeroLengthLineDirection);
         
         // remove collinear segments (stroke outline is always closed geometry, so second argument is true)
         RemoveCollinearSegments(strokeOutline, true);

         // triangulate stroke
         var triangulatedStroke = TriangulateSimplePolygon(strokeOutline);
         
         if (triangulatedStroke == null) return null;
         
         var vertices = new List<Vector3F>();
         vertices.AddRange(triangulatedStroke);

         return vertices;
      }

      /// <summary>
      /// Composes the outline from top / bottom parts plus outlines for caps
      /// </summary>
      /// <param name="pen">Stroke's pen</param>
      /// <param name="linePoints">Geometry points</param>
      /// <param name="topStrokeOutlinePart">Top stroke part</param>
      /// <param name="bottomStrokeOutlinePart">Bottom stroke part</param>
      /// <param name="isGeometryClosed">Is geometry closed</param>
      /// <param name="zeroLengthLineDirection">Direction for the case of zero length line</param>
      /// <returns></returns>
      private List<Vector2D> ComposeStrokeOutline(Pen pen,
         Vector3F[] linePoints,
         List<Vector2D> topStrokeOutlinePart,
         List<Vector2D> bottomStrokeOutlinePart,
         bool isGeometryClosed,
         Vector2D? zeroLengthLineDirection = null)
      {
         Vector2D startCapDirection;
         Vector2D endCapDirection;
         
         if (zeroLengthLineDirection == null)
         {
            // normal case
            startCapDirection = new LineSegment2D(linePoints[1], linePoints[0]).DirectionNormalized;
            endCapDirection = new LineSegment2D(linePoints[^2], linePoints[^1]).DirectionNormalized;
         }
         else
         {
            // end cap direction is similar to line direction, start cap direction is opposite to line direction
            endCapDirection = (Vector2D)zeroLengthLineDirection;
            startCapDirection = -endCapDirection;
         }

         if (!isGeometryClosed)
         {
            AddCapOutline(pen.StartLineCap, (Vector2D)linePoints[0], startCapDirection, topStrokeOutlinePart, bottomStrokeOutlinePart, pen.Thickness, true);
            AddCapOutline(pen.EndLineCap, (Vector2D)linePoints[^1], endCapDirection, topStrokeOutlinePart, bottomStrokeOutlinePart, pen.Thickness, false);
         }

         // reverse the bottom part, so that the order of points in stroke outline will be correct
         bottomStrokeOutlinePart.Reverse();

         var strokeOutline = new List<Vector2D>();
         
         strokeOutline.AddRange(topStrokeOutlinePart);
         strokeOutline.AddRange(bottomStrokeOutlinePart);

         return strokeOutline;
      }

      /// <summary>
      /// Adds cap outline to stroke outline so that they can be triangulated all together as one outline
      /// </summary>
      /// <param name="capType">Cap type</param>
      /// <param name="geometryEnd">Geometry end point</param>
      /// <param name="capDirection">Direction of the cap</param>
      /// <param name="topStrokeOutlinePart">Top stroke outline part</param>
      /// <param name="bottomStrokeOutlinePart">Bottom stroke outline part</param>
      /// <param name="thickness">Thickness of stroke</param>
      /// <param name="startCap">If start cap requested</param>
      private void AddCapOutline(PenLineCap capType, Vector2D geometryEnd, Vector2D capDirection, List<Vector2D> topStrokeOutlinePart,
         List<Vector2D> bottomStrokeOutlinePart, double thickness, bool startCap)
      {
         // if cap type is Flat - return null
         if (capType == PenLineCap.Flat) return;

         // determine base points
         //   X------   top outline part
         //   |
         //   X------   geometry line (basePoint0 or geometryEnd)
         //   |
         //   X------   bottom outline part
         var basePoint1 = startCap ? bottomStrokeOutlinePart.First() : topStrokeOutlinePart.Last();
         var basePoint2 = startCap ? topStrokeOutlinePart.First() : bottomStrokeOutlinePart.Last();

         // generate cap outline
         var capOutline = GenerateCapOutline(capType, geometryEnd, basePoint1, basePoint2, capDirection, thickness);

         if (capOutline == null) return;

         // add cap outline to stroke outline
         if (startCap)
         {
            // add to the start of top part
            topStrokeOutlinePart.InsertRange(0, capOutline);
         }
         else
         {
            // add to the end of top part
            topStrokeOutlinePart.AddRange(capOutline);
         }
      }

      /// <summary>
      /// Generates outline for stroke cap
      /// </summary>
      /// <param name="capType">Type of the cap</param>
      /// <param name="basePoint0">End point of geometry (according to what we are processing - start cap or end cap)</param>
      /// <param name="basePoint1">First of two end points of current stroke's end</param>
      /// <param name="basePoint2">Second of two end points of current stroke's end</param>
      /// <param name="capDirection">Direction in which cap should be drawn</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <returns>Points of the cap's outline</returns>
      private List<Vector2D> GenerateCapOutline(PenLineCap capType, Vector2D basePoint0, Vector2D basePoint1, Vector2D basePoint2, Vector2D capDirection, double thickness)
      {
         var outline = new List<Vector2D>();

         switch (capType)
         {
            case PenLineCap.ConvexTriangle:
            {
               var capPoint0 = basePoint0 + thickness / 2.0 * capDirection;

               outline.Add(capPoint0);

               break;
            }
            case PenLineCap.ConcaveTriangle:
            {
               var capPoint1 = basePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = basePoint2 + thickness / 2.0 * capDirection;
               
               outline.Add(capPoint1);
               outline.Add(basePoint0);
               outline.Add(capPoint2);

               break;
            }
            case PenLineCap.Square:
            {
               var capPoint1 = basePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = basePoint2 + thickness / 2.0 * capDirection;

               outline.Add(capPoint1);
               outline.Add(capPoint2);

               break;
            }
            case PenLineCap.ConvexRound:
            {
               /*var capPoint1 = basePoint1 + thickness * 0.70 * capDirection;
               var capPoint2 = basePoint2 + thickness * 0.70 * capDirection;
               var roundPoints = MathHelper.GetCubicBezier(basePoint1, capPoint1, capPoint2, basePoint2, SampleRate);*/

               var roundPoints = MathHelper.GetArcPoints(basePoint1, basePoint2, thickness / 2.0, true, ArcSampleRate);

               // remove base points from cap outline - they are already included into stroke outline
               roundPoints.RemoveAt(0);
               roundPoints.RemoveAt(roundPoints.Count - 1);
               
               outline.AddRange(roundPoints);

               break;
            }
            case PenLineCap.ConcaveRound:
            {
               var capPoint1 = basePoint1 + thickness / 2.0 * capDirection;
               var capPoint2 = basePoint2 + thickness / 2.0 * capDirection;

               //var roundPoints = MathHelper.GetCubicBezier(capPoint1, basePoint1, basePoint2, capPoint2, SampleRate);

               var roundPoints = MathHelper.GetArcPoints(capPoint1, capPoint2, thickness / 2.0, false, ArcSampleRate);

               outline.AddRange(roundPoints);

               break;
            }
            default:
               return null;
         }

         return outline;
      }

      /// <summary>
      /// Generates top and bottom stroke outline segments based on geometry segment
      /// </summary>
      /// <param name="startPoint">Start point of geometry segment</param>
      /// <param name="endPoint">End point of geomentry segment</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <returns>Array of two stroke outline segments</returns>
      private LineSegment2D[] GenerateTopBottomSegments(Vector2D startPoint, Vector2D endPoint, double thickness)
      {
         var normal = GenerateNormalToSegment(startPoint, endPoint);

         var startTopBottomPoints = GenerateTopBottomPoints(startPoint, thickness, normal);
         var endTopBottomPoints = GenerateTopBottomPoints(endPoint, thickness, normal);

         var top = new LineSegment2D(startTopBottomPoints[0], endTopBottomPoints[0]);
         var bottom = new LineSegment2D(startTopBottomPoints[1], endTopBottomPoints[1]);

         return new[] { top, bottom };
      }
      
      /// <summary>
      /// Generates top and bottom stroke outline points for given geometry point
      /// </summary>
      /// <param name="point">Geometry point</param>
      /// <param name="thickness">Thickness of the stroke</param>
      /// <param name="normal">Normal to geometry point</param>
      /// <returns>Array of two stroke outline points</returns>
      private Vector2D[] GenerateTopBottomPoints(Vector2D point, double thickness, Vector2D normal)
      {
         return new[]
         {
            RoundVector2D(point - thickness / 2.0 * normal, doublePrecision),
            RoundVector2D(point + thickness / 2.0 * normal, doublePrecision)
         };
      }

      /// <summary>
      /// Generates normal to segment
      /// </summary>
      /// <param name="startPoint">Start point of segment</param>
      /// <param name="endPoint">End point of segment</param>
      /// <returns>Normal to segment</returns>
      private Vector2D GenerateNormalToSegment(Vector2D startPoint, Vector2D endPoint)
      {
         var dir = endPoint - startPoint;
         var normal = new Vector2D(-dir.Y, dir.X);
         normal.Normalize();

         return RoundVector2D(normal, doublePrecision);
      }

      /// <summary>
      /// Rounds Vector2D values with given precision
      /// </summary>
      /// <param name="vector">Vector to be rounded</param>
      /// <param name="precision">Precision to round with</param>
      /// <returns>Rounded vector</returns>
      private Vector2D RoundVector2D(Vector2D vector, int precision)
      {
         return new Vector2D(Math.Round(vector.X, precision), Math.Round(vector.Y, precision));
      }

      /// <summary>
      /// Triangulates simple polygon via 'ear clipping' method.
      /// Polygon should not have holes in it, and should not have collinear segments.
      /// </summary>
      /// <param name="points">Polygon outline's points</param>
      /// <returns>Points of triangles, or null if triangulation failed</returns>
      private List<Vector3F> TriangulateSimplePolygon(List<Vector2D> points)
      {
         // if there are less than 3 points - exit
         if (points.Count < 3) return null;

         var trianglesPoints = new List<Vector3F>();
         var currentIndex = 0;

         // process until only 3 vertices left in point list
         while (points.Count > 3)
         {
            // loop index around
            if (currentIndex == points.Count) currentIndex = 0;

            // calculate proper previous and next indices
            var prevIndex = currentIndex - 1;
            var nextIndex = currentIndex + 1;

            if (currentIndex == 0) prevIndex = points.Count - 1;
            else if (currentIndex == points.Count - 1) nextIndex = 0;

            // take 3 consecutive points as triangle, which will be tested if it if 'ear' or not
            var p1 = points[prevIndex];
            var p2 = points[currentIndex];
            var p3 = points[nextIndex];

            // Two rules of 'ear':
            // 1. Inner angle between segments must be less then 180 degrees
            // 2. No other points of outline must be inside p1-p2-p3 triangle
            
            // Check rule 1
            var angle = MathHelper.AngleBetween(p1, p2, p2, p3);

            if (angle <= 0)
            {
               currentIndex++;
               continue;
            }

            // Check rule 2
            var skipPoint = false;
            foreach (var point in points)
            {
               if (point == p1 || point == p2 || point == p3) continue;

               if (MathHelper.IsPointInTriangle(point, p1, p2, p3))
               {
                  skipPoint = true;
                  break;
               }
            }

            if (skipPoint)
            {
               currentIndex++;
               continue;
            }

            // found ear - add current triangle to list
            trianglesPoints.Add((Vector3F)p1);
            trianglesPoints.Add((Vector3F)p2);
            trianglesPoints.Add((Vector3F)p3);

            // clip ear - delete current point from list
            points.RemoveAt(currentIndex);

            // start the process from the beginning of the point list
            currentIndex = 0;
         }

         // add last 3 points as the final triangle
         trianglesPoints.Add((Vector3F)points[0]);
         trianglesPoints.Add((Vector3F)points[1]);
         trianglesPoints.Add((Vector3F)points[2]);

         return trianglesPoints;
      }

      /// <summary>
      /// Generates proper join between two stroke outline segments depending on the requested join type
      /// </summary>
      /// <param name="first">First of two consecutive stroke outline segments</param>
      /// <param name="second">Second of two consecutive stroke outline segments</param>
      /// <param name="strokeOutlinePart">One of two stroke outline parts - top or bottom, newly generated points will be added here</param>
      /// <param name="isTopStrokeOutlinePart">Indicates if we are dealing with top stroke outline part</param>
      /// <param name="penLineJoin">Join type</param>
      /// <exception cref="ArgumentException">This exception will be thrown on unhandled join type</exception>
      private void GenerateStrokeJoin(LineSegment2D first, LineSegment2D second, List<Vector2D> strokeOutlinePart, bool isTopStrokeOutlinePart,
         PenLineJoin penLineJoin)
      {
         if (first.End == second.Start)
         {
            strokeOutlinePart.Add(first.End);
         }
         else if (Collision2D.SegmentSegmentIntersection(ref first, ref second, out var point))
         {
            strokeOutlinePart.Add(RoundVector2D(point, doublePrecision));
         }
         else
         {
            var angle = MathHelper.AngleBetween(first, second);

            // outer case - use PenLineJoin property
            if ((angle < 180 && isTopStrokeOutlinePart) || (angle > 180 && !isTopStrokeOutlinePart))
            {
               switch (penLineJoin)
               {
                  case PenLineJoin.Bevel:
                     strokeOutlinePart.Add(first.End);
                     strokeOutlinePart.Add(second.Start);
                     break;
                  case PenLineJoin.Miter:
                  {
                     var intersection =
                        Collision2D.lineLineIntersection(first.Start, first.End, second.Start, second.End);

                     if (intersection != null)
                     {
                        strokeOutlinePart.Add(first.End);
                        strokeOutlinePart.Add(RoundVector2D((Vector2D) intersection, doublePrecision));
                        strokeOutlinePart.Add(second.Start);
                     }

                     break;
                  }
                  case PenLineJoin.Round:
                  {
                     var intersection =
                        Collision2D.lineLineIntersection(first.Start, first.End, second.Start, second.End);

                     if (intersection != null)
                     {
                        var roundPoints =
                           MathHelper.GetQuadraticBezier(first.End, (Vector2D) intersection, second.Start,
                              BezierSampleRate);
                        strokeOutlinePart.AddRange(roundPoints);
                     }

                     break;
                  }
                  default:
                     throw new ArgumentException("Unhandled stroke join type");
               }
            }
            else // inner case - construct manually
            {
               var intersection =
                  Collision2D.lineLineIntersection(first.Start, first.End, second.Start, second.End);

               if (intersection != null)
               {
                  strokeOutlinePart.Add(first.End);
                  strokeOutlinePart.Add(RoundVector2D((Vector2D) intersection, doublePrecision));
                  strokeOutlinePart.Add(second.Start);
               }
            }
         }
      }
      
      /// <summary>
      /// Removes joins between two collinear segments and thus makes it one 
      /// </summary>
      /// <param name="strokeOutline">Outline where joins between collinear segments must be removed</param>
      /// <param name="isClosedGeometry">Depending on this argument algorithm will or will not check last vs first segments</param>
      private void RemoveCollinearSegments(List<Vector2D> strokeOutline, bool isClosedGeometry)
      {
         // collinear segments are only possible if there are 3 or more points in outline
         if (strokeOutline.Count < 3) return;
         
         // due to floating point numbers calculation precision issues we will evaluate against small epsilon, not zero further in the algorithm
         var epsilon = 0.001;
         var i = 1;

         do
         {
            if (i == strokeOutline.Count) i = 0;

            var prevIndex = i - 1;
            var nextIndex = i + 1;

            if (i == 0)
            {
               prevIndex = strokeOutline.Count - 1;
            }
            else if (i == strokeOutline.Count - 1)
            {
               if (!isClosedGeometry) break;
               nextIndex = 0;
            }

            // zero length line case
            if (strokeOutline[prevIndex] == strokeOutline[i] ||
                strokeOutline[i] == strokeOutline[nextIndex])
            {
               strokeOutline.RemoveAt(i);
               continue;
            }

            var currentSegment = new LineSegment2D(strokeOutline[prevIndex], strokeOutline[i]);
            var nextSegment = new LineSegment2D(strokeOutline[i], strokeOutline[nextIndex]);
            var angle = MathHelper.AngleBetween(currentSegment, nextSegment);
            
            if (Math.Abs(0 - angle) <= epsilon ||
                Math.Abs(360 - angle) <= epsilon)
            {
               strokeOutline.RemoveAt(i);
               continue;
            }
            
            i++;
         } while (i != 1);
      }

      public override Rect Bounds { get; }
      
      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }
   }
}
