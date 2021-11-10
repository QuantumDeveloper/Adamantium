using System;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Line:Shape
   {
      private LineGeometry geometry;

      public Line()
      {
         geometry = new LineGeometry();
      }

      public override Geometry RenderGeometry => geometry;

      public static readonly AdamantiumProperty X1Property = AdamantiumProperty.Register(nameof(X1), typeof (Double),
         typeof (Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty X2Property = AdamantiumProperty.Register(nameof(X2), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty Y1Property = AdamantiumProperty.Register(nameof(Y1), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

      public static readonly AdamantiumProperty Y2Property = AdamantiumProperty.Register(nameof(Y2), typeof(Double),
         typeof(Line), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));
      
      public static readonly AdamantiumProperty LineThicknessProperty = AdamantiumProperty.Register(nameof(LineThickness), typeof(Double),
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

      protected override void OnRender(DrawingContext context)
      {
         base.OnRender(context);
         var newStart = new Point(X1, Y1); //+ Location;
         var newEnd = new Point(X2, Y2); //+ Location;

         context.BeginDraw(this);
         context.DrawLine(this, Fill, newStart, newEnd, LineThickness);
         context.EndDraw(this);

      }
   }
}
