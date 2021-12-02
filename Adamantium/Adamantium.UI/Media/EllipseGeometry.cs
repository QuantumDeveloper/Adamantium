using System;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public sealed class EllipseGeometry : Geometry
   {
      public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
         typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
         typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0));

      public static readonly AdamantiumProperty CenterProperty = AdamantiumProperty.Register(nameof(Center),
         typeof(Vector2), typeof(EllipseGeometry), new PropertyMetadata(Vector2.Zero));

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

      public Vector2 Center
      {
         get => GetValue<Vector2>(CenterProperty);
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

      public EllipseGeometry(Vector2 center, Double radiusX, Double radiusY, Double startAngle = 0, Double stopAngle = 360) :
         this(center, radiusX, radiusY, startAngle, stopAngle, Matrix4x4F.Identity)
      {
      }

      public EllipseGeometry(Vector2 center, Double radiusX, Double radiusY, Double startAngle, Double stopAngle, Matrix4x4F transform)
      {
         bounds = new Rect(center - new Vector2(radiusX, radiusY), new Size(radiusX * 2, radiusY * 2));
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
         
         var translation = Matrix4x4F.Translation((float)rect.Width/2, (float)rect.Height/2, 0);
         Mesh = Engine.Graphics.Shapes.Ellipse.GenerateGeometry(
            GeometryType.Solid, 
            EllipseType.Sector,
            new Vector2F((float)rect.Width, (float)rect.Height), 
            (float)StartAngle, 
            (float)StopAngle,
            transform: translation);
         
         StrokeMesh = Engine.Graphics.Shapes.Ellipse.GenerateGeometry(
            GeometryType.Outlined, 
            EllipseType.Sector,
            new Vector2F((float)rect.Width, (float)rect.Height), 
            (float)StartAngle, 
            (float)StopAngle,
            transform: translation);
      }

      private Rect bounds;
      public override Rect Bounds => bounds;

      public override Geometry Clone()
      {
         return null;
      }
   }
}
