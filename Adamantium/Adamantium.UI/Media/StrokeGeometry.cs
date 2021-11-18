using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public class StrokeGeometry : Geometry
   {
      private Rect bounds;
      private int lastIndex = 0;
      private const int interrupt = -1;

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
         pen.PenLineJoin = PenLineJoin.Bevel;
         var strokes = new List<Vector3F>();
         var topStrokes = new List<Vector3F>();
         var bottomStrokes = new List<Vector3F>();
         var points = geometry.StrokeMesh.Positions;
         float halfThickness = (float)pen.Thickness / 2;
         for (int i = 0; i < points.Length; ++i)
         {
            var startPoint = points[i];
            var endPoint = i == points.Length - 1 ? points[0] : points[i + 1];
            
            var dir = endPoint - startPoint;
            var normal = new Vector3F(-dir.Y, dir.X, 0);
            normal.Normalize();
            dir.Normalize();
            
            var point1 = startPoint - halfThickness * normal;
            var point2 = endPoint - halfThickness * normal;
            var point0 = startPoint + halfThickness * normal;
            var point3 = endPoint + halfThickness * normal;
            
            switch (pen.PenLineJoin)
            {
               case PenLineJoin.Miter:
                  // point1 -= dir * halfThickness;
                  // point2 += dir * halfThickness;
                  break;
               
               case PenLineJoin.Round:
                  //TODO: implement rounding angles
                  break;
            }
            
            topStrokes.Add(point0);
            topStrokes.Add(point3);
            
            bottomStrokes.Add(point1);
            bottomStrokes.Add(point2);
         }

         var topSegments = new List<LineSegment2D>();
         var bottomSegments = new List<LineSegment2D>();

         for (int i = 0; i < topStrokes.Count - 1; i+=2)
         {
            var start = (Vector2D)topStrokes[i];
            var end = (Vector2D)topStrokes[i + 1];
            var line = new LineSegment2D(start, end);
            topSegments.Add(line);
            
            start = (Vector2D)bottomStrokes[i];
            end = (Vector2D)bottomStrokes[i + 1];
            line = new LineSegment2D(start, end);
            bottomSegments.Add(line);
         }

         var indices = new List<int>();
         int index = 0;
         strokes.Add((Vector3F)bottomSegments[0].Start);
         strokes.Add((Vector3F)topSegments[0].Start);
         // indices.Add(index++);
         // indices.Add(index++);
         
         for (int i = 0; i < topSegments.Count - 1; i++)
         {
            bool isTopDisconnected = true;
            bool isBottomDisconnected = true;
            
            var bottom1 = bottomSegments[i];
            var bottom2 = bottomSegments[i + 1];
            
            var top1 = topSegments[i];
            var top2 = topSegments[i + 1];
            
            if (Collision2D.SegmentSegmentIntersection(ref bottom1, ref bottom2, out var point))
            {
               isBottomDisconnected = false;
               bottom1 = new LineSegment2D(bottom1.Start, point);
               bottom2 = new LineSegment2D(point, bottom2.End);
               bottomSegments[i] = bottom1;
               bottomSegments[i + 1] = bottom2;
               strokes.Add((Vector3F)point);
               //indices.Add(index++);
            }
            
            if (Collision2D.SegmentSegmentIntersection(ref top1, ref top2, out point))
            {
               isTopDisconnected = false;
               top1 = new LineSegment2D(top1.Start, point);
               top2 = new LineSegment2D(point, top2.End);
               topSegments[i] = top1;
               topSegments[i + 1] = top2;
               strokes.Add((Vector3F)top1.Start);
               strokes.Add((Vector3F)point);
               strokes.Add((Vector3F)bottom1.End);
               //strokes.Add((Vector3F)point);
               //indices.Add(index++);
            }

            if (isTopDisconnected)
            {
               if (pen.PenLineJoin == PenLineJoin.Bevel)
               {
                  //var line = new LineSegment2D(top1.End, top2.Start);
                  //topSegments.Insert(i+1, line);
                  strokes.Add((Vector3F)top1.Start);
                  strokes.Add((Vector3F)top1.End);
                  strokes.Add((Vector3F)bottom1.End);
                  
                  strokes.Add((Vector3F)bottom1.End);
                  strokes.Add((Vector3F)top1.End);
                  strokes.Add((Vector3F)top2.Start);
               }
            }

            if (isBottomDisconnected)
            {
               if (pen.PenLineJoin == PenLineJoin.Bevel)
               {
                  //var line = new LineSegment2D(bottom1.End, bottom2.Start);
                  //bottomSegments.Insert(i+1, line);
                  strokes.Add((Vector3F)bottom1.End);
                  
                  
                  strokes.Add((Vector3F)bottom1.End);
                  strokes.Add((Vector3F)top1.End);
                  strokes.Add((Vector3F)bottom2.Start);
               }
            }
         }
         
         Mesh.SetPositions(strokes).Optimize();
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
