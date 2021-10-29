using System;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public sealed class RectangleGeometry:Geometry
   {
      public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
         typeof (Double), typeof (RectangleGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
         typeof(Double), typeof(RectangleGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty RectProperty = AdamantiumProperty.Register(nameof(Rect),
         typeof(Rect), typeof(RectangleGeometry), new PropertyMetadata(Rect.Empty));

      public RectangleGeometry()
      {
      }

      public RectangleGeometry(RectangleGeometry copy)
      {
         Mesh = copy.Mesh;
      }

      public RectangleGeometry(Rect size, Double radiusX, Double radiusY)
      {
         CreateRectangle(size, new Size(radiusX, radiusY));
      }

      public RectangleGeometry(Rect size, Thickness corners)
      {
         CreateRectangle(size, corners);
      }

      public RectangleGeometry(Rect size, Double radiusX, Double radiusY, Matrix4x4F transformation)
      {
         Transformation = transformation;
         CreateRectangle(size, new Size(radiusX, radiusY));
      }

      public Double RadiusX
      {
         get => GetValue<Double>(RadiusXProperty);
         set => SetValue(RadiusXProperty, value);
      }

      public Double RadiusY
      {
         get => GetValue<Double>(RadiusYProperty);
         set => SetValue(RadiusYProperty, value);
      }

      public Rect Rect
      {
         get => GetValue<Rect>(RectProperty);
         set => SetValue(RectProperty, value);
      }

      internal void CreateRectangle(Rect rect, Thickness corners)
      {
         Rect = rect;
         bounds = rect;

         Point position = rect.Location;

         if (corners.Left > 0.0)
         {
            Point radius = new Point(corners.Left);
            if (radius.X > rect.Width)
            {
               radius.X = rect.Width;
            }
            if (radius.Y > rect.Height)
            {
               radius.Y = rect.Height;
            }
            //left upper corner
            CreateFilledEllipseSector(new Point(position.X + corners.Left, position.Y + corners.Left), new Point(corners.Left), 180, 270);
         }
         if (corners.Top > 0.0)
         {
            if (corners.Top > rect.Width / 2)
            {
               corners.Top = rect.Width / 2;
            }
            //right upper corner
            CreateFilledEllipseSector(new Point(position.X + rect.Width - corners.Top, position.Y + corners.Top), new Point(corners.Top), 270, 360);
         }

         if (corners.Right > 0.0)
         {
            if (corners.Right > rect.Width / 2)
            {
               corners.Right = rect.Width / 2;
            }
            //right bottom corner
            CreateFilledEllipseSector(new Point(position.X + rect.Width - corners.Right, position.Y + (rect.Height - corners.Right)), new Point(corners.Right), 0, 90);
         }

         if (corners.Bottom > 0.0)
         {
            if (corners.Bottom > rect.Width / 2)
            {
               corners.Bottom = rect.Width / 2;
            }
            //left bottom corner
            CreateFilledEllipseSector(new Point(position.X + corners.Right, position.Y + (rect.Height - corners.Right)), new Point(corners.Right), 90, 180);
         }

         /*
         if (radius.X > 0 && radius.Y > 0)
         {
            if (radius.X > rect.Width / 2)
            {
               radius.X = rect.Width / 2;
            }
            if (radius.Y > rect.Height / 2)
            {
               radius.Y = rect.Height / 2;
            }

            RadiusX = radius.X;
            RadiusY = radius.Y;

            //left upper corner
            CreateFilledEllipseSector(new Point(position.X + radius.X, position.Y + radius.Y), radius, 180, 270);

            //right upper corner
            CreateFilledEllipseSector(new Point(position.X + rect.Width - radius.X, position.Y + radius.Y), radius, 270, 360);

            //right bottom corner
            CreateFilledEllipseSector(new Point(position.X + rect.Width - radius.X, position.Y + (rect.Height - radius.Y)), radius, 0, 90);

            //left bottom corner
            CreateFilledEllipseSector(new Point(position.X + radius.X, position.Y + (rect.Height - radius.Y)), radius, 90, 180);

            if (radius.X < rect.Width / 2)
            {
               //top border
               CreateSimpleRectangle(new Rect(new Point(position.X + radius.X, position.Y),
                  new Size(rect.Width - (radius.X * 2), radius.Y)));

               //bottom border
               CreateSimpleRectangle(new Rect(
                  new Point(position.X + radius.X, position.Y + (rect.Height - radius.Y)),
                  new Size(rect.Width - (radius.X * 2), radius.Y)));

            }

            if (radius.Y < rect.Height / 2)
            {
               //right border
               //VertexArray.AddRange(
               CreateSimpleRectangle(
                  new Rect(new Point(position.X + (rect.Width - radius.X), position.Y + (radius.Y)),
                     new Size(radius.X, rect.Height - (radius.Y * 2))));

               //left border
               CreateSimpleRectangle(new Rect(new Point(position.X, position.Y + (radius.Y)),
                  new Size(radius.X, rect.Height - (radius.Y * 2))));
            }

            if (radius.X < rect.Width / 2 && radius.Y < rect.Height / 2)
            {
               //center rectangle
               CreateSimpleRectangle(new Rect(new Point(position.X + radius.X, position.Y + (radius.Y)),
                  new Size(rect.Width - (radius.X * 2), rect.Height - (radius.Y * 2))));
            }
         }
         else if (radius.X <= 0 || radius.Y <= 0)
         {
            RadiusX = 0;
            RadiusY = 0;
            //center rectangle
            CreateSimpleRectangle(rect);
         }
         */

         //VertexPositionTexture[] newGeometry = null;
         //IndicesArray.AddRange(OptimizeShape(VertexArray.ToArray(), out newGeometry));
         //VertexArray.Clear();
         //VertexArray.AddRange(newGeometry);

         // Point center = new Point(position.X + (int)(rect.Width / 2), position.Y + (int)(rect.Height / 2));
         // Size roundedRadius = new Size(rect.Width / 2, rect.Height / 2);
         // for (int i = 0; i < VertexArray.Count; i++)
         // {
         //    var vertex1 = VertexArray[i];
         //    vertex1.UV = new Vector2D(0.5f - (center.X - vertex1.Position.X) / (2 * roundedRadius.Width),
         //       0.5f - (center.Y - vertex1.Position.Y) / (2 * roundedRadius.Height));
         //    VertexArray[i] = vertex1;
         // }
      }

      internal void CreateRectangle(Rect rect, Point radius)
      {
         Rect = rect;
         bounds = rect;
         //VertexArray.Clear();
         //IndicesArray.Clear();

         Point position = rect.Location;

         if (radius.X > 0 && radius.Y > 0)
         {
            if (radius.X > rect.Width / 2)
            {
               radius.X = rect.Width / 2;
            }
            if (radius.Y > rect.Height / 2)
            {
               radius.Y = rect.Height / 2;
            }

            RadiusX = radius.X;
            RadiusY = radius.Y;

            //left upper corner
            CreateFilledEllipseSector(new Point(position.X + radius.X, position.Y + radius.Y), radius, 180, 270);

            //right upper corner
            CreateFilledEllipseSector(new Point(position.X + rect.Width - radius.X, position.Y + radius.Y), radius, 270, 360);

            //right bottom corner
            CreateFilledEllipseSector(new Point(position.X + rect.Width - radius.X, position.Y + (rect.Height - radius.Y)), radius, 0, 90);

            //left bottom corner
            CreateFilledEllipseSector(new Point(position.X + radius.X, position.Y + (rect.Height - radius.Y)), radius, 90, 180);

            if (radius.X < rect.Width / 2)
            {
               //top border
               CreateSimpleRectangle(new Rect(new Point(position.X + radius.X, position.Y),
                  new Size(rect.Width - (radius.X*2), radius.Y)));

               //bottom border
               CreateSimpleRectangle(new Rect(
                  new Point(position.X + radius.X, position.Y + (rect.Height - radius.Y)),
                  new Size(rect.Width - (radius.X*2), radius.Y)));

            }

            if (radius.Y < rect.Height / 2)
            {
               //right border
               //VertexArray.AddRange(
               CreateSimpleRectangle(
                  new Rect(new Point(position.X + (rect.Width - radius.X), position.Y + (radius.Y)),
                     new Size(radius.X, rect.Height - (radius.Y * 2))));

               //left border
               CreateSimpleRectangle(new Rect(new Point(position.X, position.Y + (radius.Y)),
                  new Size(radius.X, rect.Height - (radius.Y*2))));
            }

            if (radius.X < rect.Width / 2 && radius.Y < rect.Height / 2)
            {
               //center rectangle
               CreateSimpleRectangle(new Rect(new Point(position.X + radius.X, position.Y + (radius.Y)),
                  new Size(rect.Width - (radius.X*2), rect.Height - (radius.Y*2))));
            }
         }
         else if (radius.X <= 0 || radius.Y <= 0)
         {
            RadiusX = 0;
            RadiusY = 0;
            //center rectangle
            CreateSimpleRectangle(rect);
         }

         VertexPositionTexture[] newGeometry = null;
         // IndicesArray.AddRange(OptimizeShape(VertexArray.ToArray(), out newGeometry));
         // VertexArray.Clear();
         // VertexArray.AddRange(newGeometry);

         Point center = new Point(position.X + (int)(rect.Width / 2), position.Y + (int)(rect.Height / 2));
         Size roundedRadius = new Size(rect.Width / 2, rect.Height / 2);
         // for (int i = 0; i < VertexArray.Count; i++)
         // {
         //    var vertex1 = VertexArray[i];
         //    vertex1.UV = new Vector2D(0.5f - (center.X - vertex1.Position.X) / (2 * roundedRadius.Width),
         //       0.5f - (center.Y - vertex1.Position.Y) / (2 * roundedRadius.Height));
         //    VertexArray[i] = vertex1;
         // }
      }

      private void CreateFilledEllipseSector(Point center, Point radius, double startAngle, double endAngle)
      {
         double x1 = center.X + (radius.X * Math.Cos(startAngle));
         double y1 = center.Y + (radius.Y * Math.Sin(startAngle));

         for (double i = startAngle; i <= endAngle; i++)
         {
            double angle = Math.PI * 2 / 360 * i;

            double x2 = center.X + (radius.X * Math.Cos(angle));
            double y2 = center.Y + (radius.Y * Math.Sin(angle));

            var vertex1 = new VertexPositionTexture(center, Vector2F.Zero);
            var vertex2 = new VertexPositionTexture(new Vector3D(x1, y1, 0), Vector2F.Zero);
            var vertex3 = new VertexPositionTexture(new Vector3D(x2, y2, 0), Vector2F.Zero);

            vertex1.UV = new Vector2D(0.5f - (vertex1.Position.X - center.X) / (2 * radius.X),
               0.5f - (vertex1.Position.Y - center.Y) / (2 * radius.Y));
            vertex2.UV = new Vector2D(0.5f - (vertex2.Position.X - center.X) / (2 * radius.X),
               0.5f - (vertex2.Position.Y - center.Y) / (2 * radius.Y));
            vertex3.UV = new Vector2D(0.5f - (vertex3.Position.X - center.X) / (2 * radius.X),
               0.5f - (vertex3.Position.Y - center.Y) / (2 * radius.Y));

            // VertexArray.Add(vertex1);
            // VertexArray.Add(vertex2);
            // VertexArray.Add(vertex3);
            //
            // IndicesArray.Add(lastIndex++);
            // IndicesArray.Add(lastIndex++);
            // IndicesArray.Add(lastIndex++);

            y1 = y2;
            x1 = x2;
         }

         //IndicesArray.Add(interrupt);
      }

      private void CreateSimpleRectangle(Rect rectangle)
      {
         // VertexArray.Add(new VertexPositionTexture(rectangle.Location, Vector2F.Zero));
         // VertexArray.Add(new VertexPositionTexture(rectangle.TopRight, new Vector2F(1, 0)));
         // VertexArray.Add(new VertexPositionTexture(rectangle.BottomLeft, new Vector2F(0, 1)));
         // VertexArray.Add(new VertexPositionTexture(rectangle.BottomRight, Vector2F.One));
         //
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(lastIndex++);
         // IndicesArray.Add(interrupt);
      }

      private Rect bounds;

      public override Rect Bounds => bounds;

      public override Geometry Clone()
      {
         return new RectangleGeometry(bounds, RadiusX, RadiusY);
      }
   }
}
