using System;
using System.Collections.Generic;
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
      internal uint SampleRate { get; set; } = 15;

      public StrokeGeometry()
      {
      }

      public StrokeGeometry(Pen pen, Geometry geometry)
      {
         GenerateStroke(pen, geometry);
      }

      private void GenerateStroke(Pen pen, Geometry geometry)
      {
         pen.PenLineJoin = PenLineJoin.Miter;
         //@TODO check concave zero length triangulation
         pen.StartLineCap = PenLineCap.ConcaveRound;
         pen.EndLineCap = PenLineCap.ConcaveRound;
         var points = geometry.StrokeMesh.Positions;

         // for test purposes start
         geometry.IsClosed = false;
         var testSegs = new List<Vector3F>();

         testSegs.Add(new Vector3F(50, 10, 0));
         testSegs.Add(new Vector3F(50, 10, 0));
         //testSegs.Add(new Vector3F(260, 10, 0));
         //testSegs.Add(new Vector3F(155, 180, 0));
         //testSegs.Add(new Vector3F(200, 195, 0));
         //testSegs.Add(new Vector3F(100, 295, 0));
         //testSegs.Add(new Vector3F(90, 310, 0));

         points = testSegs.ToArray();
         // for test purposes end

         if (pen.DashStrokeArray == null || pen.DashStrokeArray.Count == 0)
         {
            GenerateStroke(points, pen, geometry.IsClosed);
         }
         else
         {
            GenerateDashStroke(points, pen, false);
         }
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

               GenerateStrokeJoin(currentTopSegments[i], currentTopSegments[nextIndex], sampledTopPart, pen.PenLineJoin);
               GenerateStrokeJoin(currentBottomSegments[i], currentBottomSegments[nextIndex], sampledBottomPart, pen.PenLineJoin);
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
      private void GenerateStroke(Vector3F[] points, Pen pen, bool isGeometryClosed, Vector2D? zeroLengthLineDirection = null)
      {
         // check if geometry is valid
         if (points.Length < 2) return;

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
               GenerateStrokeJoin(topSegments[i], topSegments[nextIndex], sampledStrokeTopOutlinePart, pen.PenLineJoin);
               GenerateStrokeJoin(bottomSegments[i], bottomSegments[nextIndex], sampledStrokeBottomOutlinePart,
                  pen.PenLineJoin);
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
         
         // remove collinear segments
         RemoveCollinearSegments(strokeOutline, isGeometryClosed);

         // triangulate stroke
         var triangulatedStroke = TriangulateSimplePolygon(strokeOutline);
         
         if (triangulatedStroke == null) return;
         
         var vertices = new List<Vector3F>();
         vertices.AddRange(triangulatedStroke);
         Mesh.SetPositions(vertices).Optimize();
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
               var capPoint0 = basePoint0 - thickness / 2.0 * capDirection;
               outline.Add(capPoint0);

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
               var capPoint1 = basePoint1 + thickness * 0.70 * capDirection;
               var capPoint2 = basePoint2 + thickness * 0.70 * capDirection;
               var roundPoints = MathHelper.GetCubicBezier(basePoint1, capPoint1, capPoint2, basePoint2, SampleRate);

               // remove base points from cap outline - they are already included into stroke outline
               roundPoints.RemoveAt(0);
               roundPoints.RemoveAt(roundPoints.Count - 1);
               
               outline.AddRange(roundPoints);
               break;
            }
            case PenLineCap.ConcaveRound:
            {
               var capPoint1 = basePoint1 - thickness * 0.70 * capDirection;
               var capPoint2 = basePoint2 - thickness * 0.70 * capDirection;
               var roundPoints = MathHelper.GetCubicBezier(basePoint1, capPoint1, capPoint2, basePoint2, SampleRate);

               // remove base points from cap outline - they are already included into stroke outline
               roundPoints.RemoveAt(0);
               roundPoints.RemoveAt(roundPoints.Count - 1);
               
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

               if (MathHelper.PointInTriangle(point, p1, p2, p3))
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
      /// <param name="penLineJoin">Join type</param>
      /// <exception cref="ArgumentException">This exception will be thrown on unhandled join type</exception>
      private void GenerateStrokeJoin(LineSegment2D first, LineSegment2D second, List<Vector2D> strokeOutlinePart,
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
            switch (penLineJoin)
            {
               case PenLineJoin.Bevel:
                  strokeOutlinePart.Add(first.End);
                  strokeOutlinePart.Add(second.Start);
                  break;
               case PenLineJoin.Miter:
               {
                  var intersection = Collision2D.lineLineIntersection(first.Start, first.End, second.Start, second.End);

                  if (intersection != null)
                  {
                     strokeOutlinePart.Add(first.End);
                     strokeOutlinePart.Add(RoundVector2D((Vector2D)intersection, doublePrecision));
                     strokeOutlinePart.Add(second.Start);
                  }

                  break;
               }
               case PenLineJoin.Round:
               {
                  var intersection = Collision2D.lineLineIntersection(first.Start, first.End, second.Start, second.End);

                  if (intersection != null)
                  {
                     var roundPoints =
                        MathHelper.GetQuadraticBezier(first.End, (Vector2D)intersection, second.Start, SampleRate);
                     strokeOutlinePart.AddRange(roundPoints);
                  }

                  break;
               }
               default:
                  throw new ArgumentException("Unhandled stroke join type");
                  break;
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

         for (var i = 1; i < strokeOutline.Count; i++)
         {
            var nextIndex = i + 1;

            if (i == strokeOutline.Count - 1)
            {
               if (!isClosedGeometry) break;
               nextIndex = 1;
            }

            var currentSegment = new LineSegment2D(strokeOutline[i - 1], strokeOutline[i]);
            var nextSegment = new LineSegment2D(strokeOutline[i], strokeOutline[nextIndex]);

            var angle = MathHelper.AngleBetween(currentSegment, nextSegment);

            if (Math.Abs(0 - angle) <= epsilon ||
                Math.Abs(360 - angle) <= epsilon)
            {
               strokeOutline.RemoveAt(i);
            }
         }
      }

      public override Rect Bounds { get; }
      
      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }
   }
}
