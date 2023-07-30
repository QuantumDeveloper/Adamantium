using System;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

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

   protected override Size MeasureOverride(Size availableSize)
   {
      var point1 = new Vector2(X1, Y1);
      var point2 = new Vector2(X2, Y2);
      var min = Vector2.Min(point1, point2);
      var max = Vector2.Max(point1, point2);
      Rect = new Rect(min, max);
      return base.MeasureOverride(availableSize);
   }

   protected override void OnRender(DrawingContext context)
   {
      var start = new Vector2(X1, Y1);
      var end = new Vector2(X2, Y2);
         
      context.DrawLine(start, end, GetPen());
   }
}