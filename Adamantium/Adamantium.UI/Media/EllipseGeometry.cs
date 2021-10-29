using System;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public sealed class EllipseGeometry : Geometry
   {
      public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
         typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
         typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty CenterProperty = AdamantiumProperty.Register(nameof(Center),
         typeof(Point), typeof(EllipseGeometry), new PropertyMetadata(Point.Zero));

      public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
         typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty StopAngleProperty = AdamantiumProperty.Register(nameof(StopAngle),
         typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)360));


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

      public Double StartAngle
      {
         get => GetValue<Double>(StartAngleProperty);
         set => SetValue(StartAngleProperty, value);
      }

      public Double StopAngle
      {
         get => GetValue<Double>(StopAngleProperty);
         set => SetValue(StopAngleProperty, value);
      }

      public Point Center
      {
         get => GetValue<Point>(CenterProperty);
         set => SetValue(CenterProperty, value);
      }

      public EllipseGeometry()
      {
      }

      public EllipseGeometry(Rect rect, Double startAngle = 0, Double stopAngle = 360)
      {
         bounds = rect;
         RadiusX = bounds.Width/2;
         RadiusY = bounds.Height/2;
         Center = rect.Center;
         StartAngle = startAngle;
         StopAngle = stopAngle;
         CreateEllipse(rect, StartAngle, StopAngle);
      }

      public EllipseGeometry(Point center, Double radiusX, Double radiusY, Double startAngle = 0, Double stopAngle = 360)
      {
         bounds = new Rect(center - new Point(radiusX, radiusY), new Size(radiusX*2, radiusY*2));
         Center = center;
         RadiusX = radiusX;
         RadiusY = radiusY;
         StartAngle = startAngle;
         StopAngle = stopAngle;
         CreateEllipse(bounds, StartAngle, StopAngle);
      }

      public EllipseGeometry(Point center, Double radiusX, Double radiusY, Double startAngle, Double stopAngle, Matrix4x4F transform)
      {
         bounds = new Rect(center - new Point(radiusX, radiusY), new Size(radiusX * 2, radiusY * 2));
         Center = center;
         RadiusX = radiusX;
         RadiusY = radiusY;
         StartAngle = startAngle;
         StopAngle = stopAngle;
         CreateEllipse(bounds, StartAngle, StopAngle);
      }

      internal void CreateEllipse(Rect rect, Double startAngle = 0, Double stopAngle = 360)
      {
         bounds = rect;
         RadiusX = rect.Width / 2;
         RadiusY = rect.Height / 2;
         Center = rect.Center;
         StartAngle = startAngle;
         StopAngle = stopAngle;
         FilledEllipse(Center, RadiusX, RadiusY, startAngle, stopAngle);
      }

      private void FilledEllipse(Point center, Double radiusX, Double radiusY, Double startAngle, Double stopAngle)
      {
         double x1 = center.X + (radiusX * Math.Cos(startAngle));
         double y1 = center.Y + (radiusY * Math.Sin(startAngle));

         for (double i = startAngle; i <= stopAngle; i++)
         {
            double angle = Math.PI * 2 / 360 * (i + 1);

            double x2 = center.X + (radiusX * Math.Cos(angle));
            double y2 = center.Y + (radiusY * Math.Sin(angle));

            var vertex1 = new VertexPositionTexture(new Vector3D(center.X, center.Y, 0), Vector2D.Zero);
            var vertex2 = new VertexPositionTexture(new Vector3D(x1, y1, 0), Vector2D.Zero);
            var vertex3 = new VertexPositionTexture(new Vector3D(x2, y2, 0), Vector2D.Zero);

            vertex1.UV = new Vector2D(0.5 + (vertex1.Position.X - center.X) / (2 * radiusX),
               0.5f - (vertex1.Position.Y - center.Y) / (2 * radiusY));
            vertex2.UV = new Vector2D(0.5 + (vertex2.Position.X - center.X) / (2 * radiusX),
               0.5f - (vertex2.Position.Y - center.Y) / (2 * radiusY));
            vertex3.UV = new Vector2D(0.5 + (vertex3.Position.X - center.X) / (2 * radiusX),
               0.5f - (vertex3.Position.Y - center.Y) / (2 * radiusY));

            // VertexArray.Add(vertex1);
            // VertexArray.Add(vertex2);
            // VertexArray.Add(vertex3);

            y1 = y2;
            x1 = x2;
         }

         Optimize();
      }

      private void Optimize()
      {
         VertexPositionTexture[] newGeometry = null;
         // IndicesArray.AddRange(OptimizeShape(VertexArray.ToArray(), out newGeometry));
         // VertexArray.Clear();
         // VertexArray.AddRange(newGeometry);
      }

      private Rect bounds;
      public override Rect Bounds => bounds;

      public override Geometry Clone()
      {
         return null;
      }
   }
}
