using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core.Collections;
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
         pen.PenLineJoin = PenLineJoin.Round;
         pen.StartLineCap = PenLineCap.Round;
         pen.EndLineCap = PenLineCap.Round;
         var points = geometry.StrokeMesh.Positions;
         //var flatPoints = new List<Vector2D>();

         // for test purposes start
         var testSegs = new List<Vector3F>();

         testSegs.Add(new Vector3F(50, 10, 0));
         testSegs.Add(new Vector3F(260, 10, 0));
         // testSegs.Add(new Vector3F(155, 180, 0));
         // testSegs.Add(new Vector3F(200, 195, 0));
         // testSegs.Add(new Vector3F(100, 295, 0));
         // testSegs.Add(new Vector3F(90, 310, 0));

         points = testSegs.ToArray();
         geometry.IsClosed = true;

         // for (var i = 0; i < points.Length; i++)
         // {
         //    //points[i] = new Vector3F((float) Math.Round(points[i].X, 4), (float) Math.Round(points[i].Y, 4), 0);
         // }

         // for test purposes end
         if (pen.DashStrokeArray == null || pen.DashStrokeArray.Count == 0)
         {
            GenerateSolidStroke(points, pen, geometry.IsClosed);
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
               var topBottomLines = GenerateTopBottomLines(currentPoint, nextDashPoint, pen.Thickness);
               
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

               ProcessPartSegments(currentTopSegments, sampledTopPart, i, nextIndex, pen.PenLineJoin);
               ProcessPartSegments(currentBottomSegments, sampledBottomPart, i, nextIndex, pen.PenLineJoin);
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

            var capPoints = GenerateLineCaps(pen, dashPoints.ToArray());
            if (capPoints != null)
            {
               vertices.AddRange(capPoints);
            }
         }

         Mesh.SetPositions(vertices).Optimize();
      }

      private void GenerateSolidStroke(Vector3F[] points, Pen pen, bool isGeometryClosed)
      {
         var topSegments = new List<LineSegment2D>();
         var bottomSegments = new List<LineSegment2D>();

         for (int i = 0; i < points.Length; ++i)
         {
            var nextIndex = i + 1;

            if (i == points.Length - 1)
            {
               if (!isGeometryClosed) break;
               nextIndex = 0;
            }

            var topBottomLines =
               GenerateTopBottomLines((Vector2D)points[i], (Vector2D)points[nextIndex], pen.Thickness);
            bottomSegments.Add(topBottomLines[1]);
            topSegments.Add(topBottomLines[0]);
         }

         var sampledTopPart = new List<Vector2D>();
         var sampledBottomPart = new List<Vector2D>();
         var strokeOutline = new List<Vector2D>();

         sampledTopPart.Add(topSegments[0].Start);
         sampledBottomPart.Add(bottomSegments[0].Start);

         for (int i = 0; i < topSegments.Count; i++)
         {
            var nextIndex = i + 1;

            if (i == topSegments.Count - 1)
            {
               if (!isGeometryClosed) break;
               nextIndex = 0;
            }

            ProcessPartSegments(topSegments, sampledTopPart, i, nextIndex, pen.PenLineJoin);
            ProcessPartSegments(bottomSegments, sampledBottomPart, i, nextIndex, pen.PenLineJoin);
         }

         if (!isGeometryClosed)
         {
            sampledTopPart.Add(topSegments[^1].End);
            sampledBottomPart.Add(bottomSegments[^1].End);

            strokeOutline.AddRange(sampledTopPart);
            sampledBottomPart.Reverse();
            strokeOutline.AddRange(sampledBottomPart);
         }
         else
         {
            sampledBottomPart[0] = sampledBottomPart[^1];
            sampledTopPart[0] = sampledTopPart[^1];
            strokeOutline.AddRange(sampledTopPart);
            sampledBottomPart.Reverse();
            strokeOutline.AddRange(sampledBottomPart);
         }

         RemoveCollinearSegments(strokeOutline, isGeometryClosed);

         var vertices = new List<Vector3F>();

         // ear clipping triangulation ( https://www.youtube.com/watch?v=QAdfkylpYwc )
         var strokePoints = TriangulateSimplePolygon(strokeOutline);

         if (strokePoints != null)
         {
            vertices.AddRange(strokePoints);
         }

         if (!isGeometryClosed)
         {
            var capPoints = GenerateLineCaps(pen, points);
            if (capPoints != null)
            {
               vertices.AddRange(capPoints);
            }
         }

         Mesh.SetPositions(vertices).Optimize();
      }

      private List<Vector3F> GenerateLineCaps(Pen pen, Vector3F[] linePoints)
      {
         if (linePoints.Length < 2) return null;

         var caps = new List<Vector3F>();

         var startSegment = new LineSegment2D(linePoints[1], linePoints[0]);
         var endSegment = new LineSegment2D(linePoints[^2], linePoints[^1]);
         var startSegmentNormal = GenerateNormalToSegment(startSegment.Start, startSegment.End);
         var endSegmentNormal = GenerateNormalToSegment(endSegment.Start, endSegment.End);

         var startNorm = startSegment.DirectionNormalized;
         var endNorm = endSegment.DirectionNormalized;

         var startBasePoints = GenerateTopBottomPoints(startSegment.End, pen.Thickness, startSegmentNormal);
         var endBasePoints = GenerateTopBottomPoints(endSegment.End, pen.Thickness, endSegmentNormal);

         var startCapEnd = startSegment.End + startNorm * pen.Thickness / 2.0;
         var endCapEnd = endSegment.End + endNorm * pen.Thickness / 2.0;

         var startCapOutline = GenerateCapOutline(pen.StartLineCap, startBasePoints[0], startBasePoints[1], startCapEnd,
            startNorm, pen.Thickness);
         var endCapOutline = GenerateCapOutline(pen.EndLineCap, endBasePoints[0], endBasePoints[1], endCapEnd, endNorm,
            pen.Thickness);

         if (startCapOutline != null)
         {
            var startCapTriangulated = TriangulateSimplePolygon(startCapOutline);

            if (startCapTriangulated != null)
            {
               caps.AddRange(startCapTriangulated);
            }
         }

         if (endCapOutline != null)
         {
            var endCapTriangulated = TriangulateSimplePolygon(endCapOutline);

            if (endCapTriangulated != null)
            {
               caps.AddRange(endCapTriangulated);
            }
         }

         return caps.Count > 0 ? caps : null;
      }

      private List<Vector2D> GenerateCapOutline(PenLineCap capType, Vector2D basePoint1, Vector2D basePoint2,
         Vector2D capEnd, Vector2D capNormal, double thickness)
      {
         var outline = new List<Vector2D>();

         switch (capType)
         {
            case PenLineCap.Triangle:
               outline.Add(basePoint1);
               outline.Add(capEnd);
               outline.Add(basePoint2);
               break;
            case PenLineCap.Square:
            {
               var capPoint1 = basePoint1 + thickness / 2.0 * capNormal;
               var capPoint2 = basePoint2 + thickness / 2.0 * capNormal;

               outline.Add(basePoint1);
               outline.Add(capPoint1);
               outline.Add(capPoint2);
               outline.Add(basePoint2);

               break;
            }
            case PenLineCap.Round:
            {
               var capPoint1 = basePoint1 + thickness * 0.70 * capNormal;
               var capPoint2 = basePoint2 + thickness * 0.70 * capNormal;
               var roundPoints = MathHelper.GetCubicBezier(basePoint1, capPoint1, capPoint2, basePoint2, SampleRate);
               outline.AddRange(roundPoints);
               break;
            }
            default:
               return null;
         }

         return outline;
      }

      private LineSegment2D[] GenerateTopBottomLines(Vector2D startPoint, Vector2D endPoint, double thickness)
      {
         var normal = GenerateNormalToSegment(startPoint, endPoint);

         var startTopBottomPoints = GenerateTopBottomPoints(startPoint, thickness, normal);
         var endTopBottomPoints = GenerateTopBottomPoints(endPoint, thickness, normal);

         var bottom = new LineSegment2D(startTopBottomPoints[1], endTopBottomPoints[1]);
         var top = new LineSegment2D(startTopBottomPoints[0], endTopBottomPoints[0]);

         return new[] { top, bottom };
      }

      private Vector2D GenerateNormalToSegment(Vector2D startPoint, Vector2D endPoint)
      {
         var dir = endPoint - startPoint;
         var normal = new Vector2D(-dir.Y, dir.X);
         normal.Normalize();

         return RoundVector2D(normal, doublePrecision);
      }

      private Vector2D[] GenerateTopBottomPoints(Vector2D point, double thickness, Vector2D normal)
      {
         return new[]
         {
            RoundVector2D(point - thickness / 2.0 * normal, doublePrecision),
            RoundVector2D(point + thickness / 2.0 * normal, doublePrecision)
         };
      }

      private Vector2D RoundVector2D(Vector2D vector, int precision)
      {
         return new Vector2D(Math.Round(vector.X, precision), Math.Round(vector.Y, precision));
      }

      private List<Vector3F> TriangulateSimplePolygon(List<Vector2D> points)
      {
         // if there are less than 3 points - exit
         if (points.Count < 3) return null;

         var trianglePoints = new List<Vector3F>();
         var currentIndex = 0;

         // just process until only 3 vertices left in point list
         while (points.Count > 3)
         {
            if (currentIndex == points.Count) currentIndex = 0;

            var prevIndex = currentIndex - 1;
            var nextIndex = currentIndex + 1;

            if (currentIndex == 0) prevIndex = points.Count - 1;
            else if (currentIndex == points.Count - 1) nextIndex = 0;

            var p1 = points[prevIndex];
            var p2 = points[currentIndex];
            var p3 = points[nextIndex];

            // 1. determine the angle between segments, it must be less than 180
            var angle = MathHelper.AngleBetween(p1, p2, p2, p3);

            if (angle <= 0)
            {
               currentIndex++;
               continue;
            }

            // 2. Check if any other point is inside p1-p2-p3 triangle
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

            // 3. Found ear - clip it
            // add triangle to list
            trianglePoints.Add((Vector3F)p1);
            trianglePoints.Add((Vector3F)p2);
            trianglePoints.Add((Vector3F)p3);

            // delete current point from list
            points.RemoveAt(currentIndex);

            // start the process from beginning of point list
            currentIndex = 0;
         }

         // add last 3 points as the final triangle
         trianglePoints.Add((Vector3F)points[0]);
         trianglePoints.Add((Vector3F)points[1]);
         trianglePoints.Add((Vector3F)points[2]);

         return trianglePoints;
      }

      private void RemoveCollinearSegments(List<Vector2D> strokeOutline, bool isClosedGeometry)
      {
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

      private void ProcessPartSegments(
         List<LineSegment2D> partSegments, 
         List<Vector2D> strokePart, 
         int currentIndex,
         int nextIndex, 
         PenLineJoin penLineJoin)
      {
         var first = partSegments[currentIndex];
         var second = partSegments[nextIndex];

         AddPointsToStrokePart(first, second, strokePart, penLineJoin);
      }

      private void AddPointsToStrokePart(LineSegment2D first, LineSegment2D second, List<Vector2D> strokePart,
         PenLineJoin penLineJoin)
      {
         if (first.End == second.Start)
         {
            strokePart.Add(first.End);
         }
         else if (Collision2D.SegmentSegmentIntersection(ref first, ref second, out var point))
         {
            strokePart.Add(RoundVector2D(point, doublePrecision));
         }
         else
         {
            switch (penLineJoin)
            {
               case PenLineJoin.Bevel:
                  strokePart.Add(first.End);
                  strokePart.Add(second.Start);
                  break;
               case PenLineJoin.Miter:
               {
                  var intersection = Collision2D.lineLineIntersection(first.Start, first.End, second.Start, second.End);

                  if (intersection != null)
                  {
                     strokePart.Add(first.End);
                     strokePart.Add(RoundVector2D((Vector2D)intersection, doublePrecision));
                     strokePart.Add(second.Start);
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
                     strokePart.AddRange(roundPoints);
                  }

                  break;
               }
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
