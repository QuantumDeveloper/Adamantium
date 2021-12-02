using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public sealed class StreamGeometry : Geometry
   {
      public StreamGeometryContext context;

      public StreamGeometry()
      {
         context = new StreamGeometryContext();
      }

      public override Rect Bounds { get; }

      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }
   }

   public class StreamGeometryContext
   {
      public List<VertexPositionTexture> VertexArray = new List<VertexPositionTexture>();
      public List<Int32> IndicesArray = new List<int>();
      private Vector2 currentPosition;
      private int lastIndex = 0;
      private readonly int interrupt = -1;

      public void BeginFigure(Vector2 startPoint)
      {
         currentPosition = startPoint;
      }

      public void LineTo(Vector2 point, double thickness)
      {
         var point2 = point- currentPosition;
         var normal = new Vector2F(-(float)point2.Y, (float)point2.X);
         normal.Normalize();
         var p0 = currentPosition + normal*(float) thickness;
         var p1 = point + normal * (float)thickness;

         //top left corner
         VertexArray.Add(new VertexPositionTexture((Vector3F)currentPosition, Vector2F.Zero));
         //top right corner
         VertexArray.Add(new VertexPositionTexture((Vector3F)point, new Vector2F(1, 0)));
         //Bottom left corner
         VertexArray.Add(new VertexPositionTexture(new Vector3F(p0, 0), new Vector2F(0, 1)));
         //Bottom right corner
         VertexArray.Add(new VertexPositionTexture(new Vector3F(p1, 0), Vector2F.One));
         
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(interrupt);

         currentPosition = point;
      }

      public void ArcTo(Vector2 point, Size radius, Double thickness)
      {
         //TODO: correctly calculate point for center to produce correct results
         //var rad = new Point(point.X - radius.Width, currentPosition.Y + radius.Height);
         var rad = new Vector2(point.X - (currentPosition.X-point.X), point.Y - currentPosition.Y);
         //var rad = new Point(radius);


         var point2 = rad - currentPosition;
         var cross = new Vector2F((float)point2.Y, (float)-point2.X) * 1;
         cross.Normalize();
         var startAngle = MathHelper.RadiansToDegrees((float)Math.Atan2(cross.X, cross.Y));

         var point3 = rad - point;
         var cross2 = new Vector2F((float)point3.Y, (float)-point3.X) * 1;
         cross2.Normalize();
         var stopAngle = MathHelper.RadiansToDegrees((float)Math.Atan2(cross2.X, cross2.Y));


         double step = 1.0;
         //draw sector of the circle
         for (double i = -startAngle; i <= -stopAngle; i += step)
         {
            double angle = Math.PI * 2 / 360 * i;

            double x = rad.X + ((radius.Width - thickness) * Math.Cos(angle));
            double y = rad.Y + ((radius.Height - thickness) * Math.Sin(angle));
            VertexArray.Add(new VertexPositionTexture(new Vector3(x, y, 0), new Vector2()));
            IndicesArray.Add(lastIndex++);

            x = rad.X + ((radius.Width) * Math.Cos(angle));
            y = rad.Y + ((radius.Height) * Math.Sin(angle));
            VertexArray.Add(new VertexPositionTexture(new Vector3(x, y, 0), new Vector2()));
            IndicesArray.Add(lastIndex++);
         }
         IndicesArray.Add(interrupt);

         currentPosition = point;
      }

      public void QuadraticBezier(Vector2 point1, Vector2 point2, Vector2 point3, double thickness)
      {
         //Calculate exactly how many line segments shoud be
         var step = Math.Abs(1.0f/(point2.Length() - point1.Length()));
         currentPosition = point1;
         for (double i = step; i <= 1; i+=step)
         {
            var x = Math.Pow(1 - i, 2)*point1.X + 2*(1 - i)*i*point2.X + Math.Pow(i, 2)*point3.X;
            var y = Math.Pow(1 - i, 2) * point1.Y + 2*(1 - i) * i * point2.Y + Math.Pow(i, 2) * point3.Y;
            var current = new Vector2(x, y);

            LineTo(current, thickness);
         }
         currentPosition = point3;
      }


      public void CubicBezier(Vector2 point1, Vector2 point2, Vector2 point3, Vector2 point4, double thickness)
      {
         var step = Math.Abs(1.0f / (point2.Length() - point1.Length()));
         currentPosition = point1;
         for (double i = step; i <= 1; i += step)
         {
            var x = Math.Pow(1 - i, 3)*point1.X + 3*Math.Pow((1 - i), 2)*i*point2.X + 3*(1 - i)*Math.Pow(i, 2)*point3.X +
                    Math.Pow(i, 3)*point4.X;
            var y = Math.Pow(1 - i, 3)*point1.Y + 3*Math.Pow((1 - i), 2)*i*point2.Y + 3*(1 - i)*Math.Pow(i, 2)*point3.Y +
                    Math.Pow(i, 3)*point4.Y;
            var current = new Vector2(x, y);

            LineTo(current, thickness);
         }
         currentPosition = point3;
      }
   }
}
