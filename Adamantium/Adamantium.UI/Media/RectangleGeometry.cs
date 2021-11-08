using System;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public sealed class RectangleGeometry : Geometry
   {
      public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
         typeof (CornerRadius), typeof (RectangleGeometry), new PropertyMetadata(new CornerRadius(0)));

      public static readonly AdamantiumProperty RectProperty = AdamantiumProperty.Register(nameof(Rect),
         typeof(Rect), typeof(RectangleGeometry), new PropertyMetadata(Rect.Empty));

      public RectangleGeometry()
      {
      }

      public RectangleGeometry(RectangleGeometry copy)
      {
         Mesh = copy.Mesh;
         Rect = copy.Rect;
         CornerRadius = copy.CornerRadius;
      }

      public RectangleGeometry(Rect size, CornerRadius corners)
      {
         CreateRectangle(size, corners);
      }

      public CornerRadius CornerRadius
      {
         get => GetValue<CornerRadius>(CornerRadiusProperty);
         set => SetValue(CornerRadiusProperty, value);
      }

      public Rect Rect
      {
         get => GetValue<Rect>(RectProperty);
         set => SetValue(RectProperty, value);
      }

      internal void CreateRectangle(Rect rect, CornerRadius corners)
      {
         Rect = rect;
         bounds = rect;
         CornerRadius = corners;

         var translation = Matrix4x4F.Translation((float)rect.Width/2, (float)rect.Height/2, 0);
         Mesh = Engine.Graphics.Shapes.Rectangle.GenerateGeometry(
            GeometryType.Solid, 
            (float)rect.Width, 
            (float)rect.Height,
            CornerRadius, 
            TesselationFactor, 
            translation);
      }

      private Rect bounds;

      public override Rect Bounds => bounds;

      public override Geometry Clone()
      {
         return new RectangleGeometry(this);
      }
   }
}
