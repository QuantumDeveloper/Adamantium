using System;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Line : Shape
   {
      public Line()
      {
      }

      public static readonly AdamantiumProperty X1Property = AdamantiumProperty.Register(nameof(X1), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty X2Property = AdamantiumProperty.Register(nameof(X2), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty Y1Property = AdamantiumProperty.Register(nameof(Y1), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty Y2Property = AdamantiumProperty.Register(nameof(Y2), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty LineThicknessProperty = AdamantiumProperty.Register(
         nameof(LineThickness), typeof(Double),
         typeof(Line), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsMeasure));

      public Double X1
      {
         get => GetValue<Double>(X1Property);
         set => SetValue(X1Property, value);
      }

      public Double X2
      {
         get => GetValue<Double>(X2Property);
         set => SetValue(X2Property, value);
      }

      public Double Y1
      {
         get => GetValue<Double>(Y1Property);
         set => SetValue(Y1Property, value);
      }

      public Double Y2
      {
         get => GetValue<Double>(Y2Property);
         set => SetValue(Y2Property, value);
      }

      public Double LineThickness
      {
         get => GetValue<Double>(LineThicknessProperty);
         set => SetValue(LineThicknessProperty, value);
      }

      protected override Size MeasureOverride(Size availableSize)
      {
         var point1 = new Vector2D(X1, Y1);
         var point2 = new Vector2D(X2, Y2);
         var min = Vector2D.Min(point1, point2);
         var max = Vector2D.Max(point1, point2);
         BoundingRectangle = new Rect(min, max);
         return base.MeasureOverride(availableSize);
      }

      protected override void OnRender(DrawingContext context)
      {
         base.OnRender(context);
         var newStart = new Point(X1, Y1); //+ Location;
         var newEnd = new Point(X2, Y2); //+ Location;
         
         var pen = new Pen(
            Stroke,
            StrokeThickness,
            StrokeDashArray,
            StartLineCap,
            EndLineCap);

         context.BeginDraw(this);
         context.DrawLine(Stroke, newStart, newEnd, pen);
         context.EndDraw(this);
      }
   }
}
