using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using AdamantiumVulkan.SPIRV;
using Point = Adamantium.Mathematics.Point;

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
      { }

      public StrokeGeometry(Pen pen, Geometry geometry)
      {
         GenerateStroke(pen, geometry);
      }

      public StrokeGeometry(EllipseGeometry ellipseGeometry, Double thickness, DashStyle strokeDashArray)
      {
         CreateEllipseStroke(ellipseGeometry, thickness, strokeDashArray);
      }

      private void GenerateStroke(Pen pen, Geometry geometry)
      {
         pen.PenLineJoin = PenLineJoin.Round;
         pen.StartLineCap = PenLineCap.Round;
         pen.EndLineCap = PenLineCap.Round;
         var topSegments = new List<LineSegment2D>();
         var bottomSegments = new List<LineSegment2D>();
         var points = geometry.StrokeMesh.Positions;
         var flatPoints = new List<Vector2D>();

         // for test purposes start
         var testSegs = new List<Vector3F>();
         
         testSegs.Add(new Vector3F(50, 10, 0));
         testSegs.Add(new Vector3F(260, 10, 0));
         testSegs.Add(new Vector3F(155, 180, 0));
         testSegs.Add(new Vector3F(200, 195, 0));
         testSegs.Add(new Vector3F(100, 295, 0));
         testSegs.Add(new Vector3F(90, 310, 0));

         points = testSegs.ToArray();
         geometry.IsClosed = true;

         for (var i = 0; i < points.Length; i++)
         {
            points[i] = new Vector3F((float) Math.Round(points[i].X, 4), (float) Math.Round(points[i].Y, 4), 0);
         }

         // for test purposes end

         for (int i = 0; i < points.Length; ++i)
         {
            flatPoints.Add((Vector2D)points[i]);
            var nextIndex = i + 1;

            if (i == points.Length - 1)
            {
               if (!geometry.IsClosed) break;
               nextIndex = 0;
            }

            var topBottomLines = GenerateTopBottomLines((Vector2D)points[i], (Vector2D)points[nextIndex], pen.Thickness);
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
               if (!geometry.IsClosed) break;
               nextIndex = 0;
            }

            ProcessPartSegments(topSegments, sampledTopPart, i, nextIndex, pen.PenLineJoin);
            ProcessPartSegments(bottomSegments, sampledBottomPart, i, nextIndex, pen.PenLineJoin);
         }

         if (!geometry.IsClosed)
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

         RemoveCollinearSegments(strokeOutline, geometry.IsClosed);
         
         /*Mesh.SetPositions(strokeOutline);
         Mesh.SetTopology(PrimitiveType.LineStrip);
         Mesh.GenerateBasicIndices();*/

         var vertices = new List<Vector3F>();
         
         // ear clipping triangulation ( https://www.youtube.com/watch?v=QAdfkylpYwc )
         var strokePoints = TriangulateSimplePolygon(strokeOutline);

         if (strokePoints != null)
         {
            vertices.AddRange(strokePoints);
         }

         if (!geometry.IsClosed)
         {
            var capPoints = GenerateLineCaps(pen, flatPoints);
            if (capPoints != null)
            {
               vertices.AddRange(capPoints);
            }
         }

         if (vertices.Count > 0)
         {
            Mesh.SetPositions(vertices).Optimize();
         }
      }

      private List<Vector3F> GenerateLineCaps(Pen pen, List<Vector2D> linePoints)
      {
         if (linePoints.Count < 2) return null;

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

         var startCapOutline = GenerateCapOutline(pen.StartLineCap, startBasePoints[0], startBasePoints[1], startCapEnd, startNorm, pen.Thickness);
         var endCapOutline = GenerateCapOutline(pen.EndLineCap, endBasePoints[0], endBasePoints[1], endCapEnd, endNorm, pen.Thickness);

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

      private List<Vector2D> GenerateCapOutline(PenLineCap capType, Vector2D basePoint1, Vector2D basePoint2, Vector2D capEnd, Vector2D capNormal, double thickness)
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
         
         return new [] { top, bottom };
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
         return new []
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

      private void ProcessPartSegments(List<LineSegment2D> partSegments, List<Vector2D> strokePart, int currentIndex, int nextIndex, PenLineJoin penLineJoin)
      {
         var first = partSegments[currentIndex];
         var second = partSegments[nextIndex];
         
         AddPointsToStrokePart(first, second, strokePart, penLineJoin);
      }
      
      private void AddPointsToStrokePart(LineSegment2D first, LineSegment2D second, List<Vector2D> strokePart, PenLineJoin penLineJoin)
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
                     var roundPoints = MathHelper.GetQuadraticBezier(first.End, (Vector2D) intersection, second.Start, SampleRate);
                     strokePart.AddRange(roundPoints);
                  }

                  break;
               }
            }
         }
      }
      
      internal void CreateRectangleStroke(RectangleGeometry geometry, Double thickness, DashStyle dashStyle)
      {
         lastIndex = 0;
         RectangleStroke(geometry.Bounds, geometry.CornerRadius, thickness, dashStyle);
      }

      internal void CreateEllipseStroke(EllipseGeometry ellipseGeometry, Double thickness, DashStyle strokeDashArray)
      {
         lastIndex = 0;
         CircleStroke(ellipseGeometry.Center, new Size(ellipseGeometry.RadiusX + thickness, ellipseGeometry.RadiusY + thickness),
            ellipseGeometry.StartAngle, ellipseGeometry.StopAngle, thickness, strokeDashArray);
      }

      private void RectangleStroke(Rect bounds, CornerRadius cornerRadius, Double thickness, DashStyle dashStyle)
      {
         /*
         if (dashStyle?.Dashes == null || dashStyle.Dashes.Count == 0)
         {
            
            //Create top stroke
            Rect rect;
            if (radiusX > 0)
            {
               rect = new Rect(new Point(bounds.TopLeft.X + radiusX, bounds.TopLeft.Y - thickness),
                  new Size(bounds.Width - radiusX*2, thickness));
            }
            else
            {
               rect = new Rect(new Point(bounds.TopLeft.X + radiusX, bounds.TopLeft.Y - thickness),
                  new Size(bounds.Width - radiusX*2 + thickness, thickness));
            }
            SimpleRectangle(rect);

            //Create bottom stroke
            if (radiusX > 0)
            {
               rect = new Rect(new Point(bounds.BottomLeft.X + radiusX, bounds.BottomLeft.Y),
                  new Size(bounds.Width - radiusX*2, thickness));
            }
            else
            {
               rect = new Rect(new Point(bounds.BottomLeft.X + radiusX - thickness, bounds.BottomLeft.Y),
                  new Size(bounds.Width - radiusX*2 + thickness, thickness));
            }
            SimpleRectangle(rect);

            //Create right stroke
            if (radiusY > 0)
            {
               rect = new Rect(new Point(bounds.TopRight.X, bounds.TopRight.Y + radiusY),
                  new Size(thickness, bounds.Height - radiusY*2));
            }
            else
            {
               rect = new Rect(new Point(bounds.TopRight.X, bounds.TopRight.Y + radiusY),
                  new Size(thickness, bounds.Height - radiusY*2 + thickness));
            }
            SimpleRectangle(rect);

            //Create left stroke
            if (radiusY > 0)
            {
               rect = new Rect(new Point(bounds.TopLeft.X - thickness, bounds.TopLeft.Y + radiusY),
                  new Size(thickness, bounds.Height - radiusY*2));
            }
            else
            {
               rect = new Rect(bounds.TopLeft - thickness,
                  new Size(thickness, bounds.Height - radiusY*2 + thickness));
            }
            SimpleRectangle(rect);

         }
         else
         {
            DashedRectangle(bounds, new Point(radiusX, radiusY), dashStyle, thickness);
         }

         if (radiusX > 0 && radiusY > 0)
         {
            //Calculate Top Left arc
            double centerX = bounds.TopLeft.X + radiusX;
            double centerY = bounds.TopLeft.Y + radiusY;
            Size radius = new Size(radiusX + thickness, radiusY + thickness);
            CircleStroke(new Point(centerX, centerY), radius, 180, 270, thickness, dashStyle);

            //Calculate Top Rigth arc
            centerX = bounds.TopRight.X - radiusX;
            centerY = bounds.TopRight.Y + radiusY;
            CircleStroke(new Point(centerX, centerY), radius, 270, 360, thickness, dashStyle);

            //Calculate Bottom Right arc
            centerX = bounds.BottomRight.X - radiusX;
            centerY = bounds.BottomRight.Y - radiusY;
            CircleStroke(new Point(centerX, centerY), radius, 0, 90, thickness, dashStyle);

            //Calculate Bottom Left arc
            centerX = bounds.BottomLeft.X + radiusX;
            centerY = bounds.BottomLeft.Y - radiusY;
            CircleStroke(new Point(centerX, centerY), radius, 90, 180, thickness, dashStyle);
         }*/
      }


      private void DashedRectangle(Rect rect, Point radius, DashStyle dashStyle, Double thickness)
      {
         List<Double> dashLength = new List<double>();
         List<Double> spaceLength = new List<double>();
         for (int i = 0; i < dashStyle.Dashes.Count; i++)
         {
            if (i % 2 == 0)
            {
               dashLength.Add(dashStyle.Dashes[i]);
            }
            else
            {
               spaceLength.Add(dashStyle.Dashes[i]);
            }
         }

         Double width = rect.Width - (radius.X * 2);
         Double height = rect.Height - (radius.Y * 2);
         Point currentPosition = new Point(rect.TopLeft.X + radius.X, rect.TopLeft.Y - thickness);
         double remainingLength = 0;
         Side currentSide = Side.Top;
         double tmpHeight = height;// + thickness;
         double tmpWidth = width;// + thickness;

         while (currentSide != Side.None)
         {
            for (int k = 0; k < dashLength.Count; k++)
            {
               switch (currentSide)
               {
                  case Side.Top:
                     //Create top stroke
                     var rect1 = new Rect(currentPosition,
                        new Size(dashLength[k], thickness));
                     if (tmpWidth < rect1.Width)
                     {
                        remainingLength = rect1.Width - tmpWidth;
                        rect1.Width = tmpWidth;

                        currentSide = Side.Right;
                        tmpWidth = width;
                        break;
                     }
                     tmpWidth -= (rect1.Width + spaceLength[k]);
                     SimpleRectangle(rect1);
                     currentPosition.X += dashLength[k] + spaceLength[k];
                     if (currentPosition.X > rect.TopRight.X)
                     {
                        currentPosition.X = rect.TopRight.X;
                     }
                     break;
                  case Side.Right:
                  {
                     if (currentPosition.Y < rect.TopRight.Y + radius.Y)
                     {
                        currentPosition.Y = rect.TopRight.Y + radius.Y;
                     }

                     if (currentPosition.X < rect.TopRight.X)
                     {
                        currentPosition.X = rect.TopRight.X;
                     }

                     //Create right stroke
                     if (remainingLength > 0)
                     {
                        rect1 = new Rect(currentPosition, new Size(thickness, remainingLength));
                        currentPosition.Y += remainingLength + spaceLength[k];
                        remainingLength = 0;
                     }
                     else
                     {
                        rect1 = new Rect(currentPosition, new Size(thickness, dashLength[k]));
                        currentPosition.Y += dashLength[k] + spaceLength[k];
                     }

                     if (tmpHeight < rect1.Height)
                     {
                        remainingLength = rect1.Height - tmpHeight;
                        rect1.Height = tmpHeight;
                        currentSide = Side.Bottom;
                        tmpHeight = height;
                        break;
                     }
                     SimpleRectangle(rect1);
                     tmpHeight -= (rect1.Height + spaceLength[k]);
                  }
                     break;
                  case Side.Bottom:
                  {
                     if (currentPosition.Y != rect.BottomRight.Y)
                     {
                        currentPosition.Y = rect.BottomRight.Y;
                     }

                     if (currentPosition.X > rect.BottomRight.X - radius.X)
                     {
                        currentPosition.X = rect.BottomRight.X - radius.X;
                     }

                     //Create bottom stroke
                     if (remainingLength > 0)
                     {
                        rect1 = new Rect(new Point(currentPosition.X - remainingLength, currentPosition.Y),
                           new Size(remainingLength, thickness));
                        currentPosition.X -= (remainingLength + spaceLength[k]);
                        remainingLength = 0;
                     }
                     else
                     {
                        rect1 = new Rect(new Point(currentPosition.X - dashLength[k], currentPosition.Y),
                           new Size(dashLength[k], thickness));
                        currentPosition.X -= (dashLength[k] + spaceLength[k]);
                     }

                     if (tmpWidth < rect1.Width)
                     {
                        remainingLength = rect1.Width - tmpWidth;
                        rect1.Width = tmpWidth;
                        currentSide = Side.Left;
                        tmpWidth = width;
                        break;
                     }
                     tmpWidth -= (rect1.Width + spaceLength[k]);
                     SimpleRectangle(rect1);

                     if (currentPosition.X < rect.BottomLeft.X + radius.X - thickness)
                     {
                        currentPosition.X = rect.BottomLeft.X + radius.X - thickness;
                     }
                  }
                     break;

                  case Side.Left:
                  {
                     if (currentPosition.X > rect.BottomLeft.X - thickness)
                     {
                        currentPosition.X = rect.BottomLeft.X - thickness;
                     }

                     if (currentPosition.Y > rect.BottomLeft.Y - radius.Y)
                     {
                        currentPosition.Y = rect.BottomLeft.Y - radius.Y;
                     }

                     if (remainingLength > 0)
                     {
                        //Create left stroke
                        rect1 = new Rect(new Point(currentPosition.X, currentPosition.Y - remainingLength),
                           new Size(thickness, remainingLength));
                        currentPosition.Y -= (remainingLength + spaceLength[k]);
                        remainingLength = 0;
                     }
                     else
                     {
                        //Create left stroke
                        rect1 = new Rect(new Point(currentPosition.X, currentPosition.Y - dashLength[k]),
                           new Size(thickness, dashLength[k]));
                        currentPosition.Y -= (dashLength[k] + spaceLength[k]);
                     }

                     if (tmpHeight < rect1.Height)
                     {
                        rect1.Height = tmpHeight;
                        currentSide = Side.None;
                        break;
                     }

                     tmpHeight -= (rect1.Height + spaceLength[k]);
                     SimpleRectangle(rect1);
                     
                  }
                     break;
               }
            }
         }
      }

      private void SimpleRectangle(Rect rect)
      {
         // VertexArray.Add(new VertexPositionTexture(rect.TopLeft, Vector2F.One));
         // VertexArray.Add(new VertexPositionTexture(rect.TopRight, Vector2F.One));
         // VertexArray.Add(new VertexPositionTexture(rect.BottomLeft, Vector2F.One));
         // VertexArray.Add(new VertexPositionTexture(rect.BottomRight, Vector2F.One));
         //
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(interrupt);
      }

      /// <summary>
      /// Create array of points representing an empty 2D circle sector with stroke thickness
      /// </summary>
      /// <param name="center">center of the circle</param>
      /// <param name="radius">circle radius (must include thickness)</param>
      /// <param name="startAngle">start angle in degrees</param>
      /// <param name="stopAngle">end angle in degrees</param>
      /// <param name="thickness">circle strock thickness</param>
      /// <param name="dashStyle"></param>
      /// <returns>array of type <see cref="VertexPositionColor"/></returns>
      /// <remarks> To draw it, use TriangleStrip primitive type</remarks>
      private void CircleStroke(Point center, Size radius, double startAngle, double stopAngle, double thickness, DashStyle dashStyle)
      {
         if (dashStyle == null || dashStyle.Dashes == null || dashStyle.Dashes?.Count == 0)
         {
            double step = 1.0;
            //draw sector of the circle
            for (double i = startAngle; i <= stopAngle; i += step)
            {
               double angle = Math.PI * 2 / 360 * i;

               double x = center.X + ((radius.Width - thickness) * Math.Cos(angle));
               double y = center.Y + ((radius.Height - thickness) * Math.Sin(angle));
               //VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
               //IndicesArray.Add(lastIndex++);

               x = center.X + ((radius.Width) * Math.Cos(angle));
               y = center.Y + ((radius.Height) * Math.Sin(angle));
               //VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
               //IndicesArray.Add(lastIndex++);
            }
            //IndicesArray.Add(interrupt);
         }

         else
         {
            List<Double> dashLength = new List<double>();
            List<Double> spaceLength = new List<double>();
            for (int i = 0; i < dashStyle.Dashes.Count; i++)
            {
               if (i % 2 == 0)
               {
                  dashLength.Add(dashStyle.Dashes[i]);
               }
               else
               {
                  spaceLength.Add(dashStyle.Dashes[i]);
               }
            }

            //int index = 0;
            double cumulativeAngle = startAngle;
            while (cumulativeAngle < stopAngle)
            {
               for (int k = 0; k < dashLength.Count; k++)
               {
                  double step = 1.0;
                  if (cumulativeAngle >= stopAngle)
                  {
                     break;
                  }
                  double end = cumulativeAngle + dashLength[k];
                  if (end > stopAngle)
                  {
                     end = stopAngle;
                  }

                  for (double i = cumulativeAngle; i <= end; i += step)
                  {
                     double angle = Math.PI * 2 / 360 * i;

                     double x = center.X + ((radius.Width - thickness) * Math.Cos(angle));
                     double y = center.Y + ((radius.Height - thickness) * Math.Sin(angle));
                     //VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
                     //IndicesArray.Add(lastIndex);
                     lastIndex++;

                     x = center.X + ((radius.Width) * Math.Cos(angle));
                     y = center.Y + ((radius.Height) * Math.Sin(angle));
                     //VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
                     //IndicesArray.Add(lastIndex);

                     lastIndex++;
                  }

                  //IndicesArray.Add(interrupt);
                  cumulativeAngle += dashLength[k];
                  cumulativeAngle += spaceLength[k];
                  if (cumulativeAngle > stopAngle)
                  {
                     cumulativeAngle = stopAngle;
                  }

               }
            }
         }

      }

      public override Rect Bounds { get; }
      public override Geometry Clone()
      {
         return null;
      }
   }
}
