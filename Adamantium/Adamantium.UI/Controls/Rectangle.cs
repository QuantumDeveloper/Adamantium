using System;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Rectangle:Shape
   {
      public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
         typeof(Double), typeof(Rectangle),
         new PropertyMetadata((Double)0, PropertyMetadataOptions.BindsTwoWayByDefault|PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
         typeof(Double), typeof(Rectangle),
         new PropertyMetadata((Double)0, PropertyMetadataOptions.BindsTwoWayByDefault|PropertyMetadataOptions.AffectsRender));

      private RectangleGeometry geometry;

      public Rectangle()
      {
         geometry = new RectangleGeometry();
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

      public override Geometry RenderGeometry => geometry;

      //protected override Size MeasureOverride(Size availableSize)
      //{
      //   if (Stretch == Stretch.UniformToFill)
      //   {
      //      double width = availableSize.Width;
      //      double height = availableSize.Height;

      //      if (Double.IsInfinity(width) && Double.IsInfinity(height))
      //      {
      //         return new Size(StrokeThickness, StrokeThickness);
      //      }
      //      if (Double.IsInfinity(width) || Double.IsInfinity(height))
      //      {
      //         width = Math.Min(width, height);
      //      }
      //      else
      //      {
      //         width = Math.Max(width, height);
      //      }

      //      return new Size(width, width);
      //   }
      //   return new Size(StrokeThickness, StrokeThickness);
      //}

      protected override void OnRender(DrawingContext context)
      {
         base.OnRender(context);
         geometry = new RectangleGeometry(new Rect(new Size(ActualWidth, ActualHeight)).Deflate(StrokeThickness),RadiusX, RadiusY);
         context.BeginDraw(this);
         context.DrawGeometry(this, Fill, new Pen(Stroke, StrokeThickness, null, StrokeDashCap, StartLineCap,
            EndLineCap), geometry);
         context.EndDraw(this);

      }
   }
}
