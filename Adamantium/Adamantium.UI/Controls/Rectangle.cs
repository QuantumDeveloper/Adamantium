using System;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Rectangle : Shape
   {
      public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
         typeof(CornerRadius), typeof(Rectangle),
         new PropertyMetadata(new CornerRadius(0),
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender));

      private RectangleGeometry geometry;

      public Rectangle()
      {
         geometry = new RectangleGeometry();
      }

      public CornerRadius CornerRadius
      {
         get => GetValue<CornerRadius>(CornerRadiusProperty);
         set => SetValue(CornerRadiusProperty, value);
      }

      public override Geometry RenderGeometry => geometry;

      protected override void OnRender(DrawingContext context)
      {
         base.OnRender(context);
         context.BeginDraw(this);
         var dstRect = Rect.Deflate(StrokeThickness);
         var pen = new Pen(Stroke, StrokeThickness, null, StrokeDashCap, StartLineCap, EndLineCap);
         context.DrawRectangle(this, Fill, dstRect, CornerRadius, pen);
         context.EndDraw(this);

      }
   }
}
