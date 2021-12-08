using System;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Ellipse : Shape
   {
      public Ellipse()
      {
      }

      public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
         typeof(Double), typeof(Ellipse),
         new PropertyMetadata((Double)0,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender,
            StartAngleValueCallback));

      public static readonly AdamantiumProperty StopAngleProperty = AdamantiumProperty.Register(nameof(StopAngle),
         typeof(Double), typeof(Ellipse),
         new PropertyMetadata((Double)360,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender,
            StopAngleValueCallback));

      private static object StopAngleValueCallback(AdamantiumComponent adamantiumObject, object baseValue)
      {
         if (adamantiumObject is Ellipse ellipse)
         {
            var stopAngle = (Double)baseValue;
            if (stopAngle > 360.0)
            {
               return 360.0;
            }

            if (stopAngle < ellipse.StartAngle)
            {
               return ellipse.StartAngle;
            }
         }

         return baseValue;
      }

      private static object StartAngleValueCallback(AdamantiumComponent adamantiumObject, object baseValue)
      {
         if (adamantiumObject is Ellipse ellipse)
         {
            var startAngle = (Double)baseValue;
            if (startAngle < 0)
            {
               return 0.0;
            }

            if (startAngle > ellipse.StopAngle)
            {
               return ellipse.StopAngle;
            }
         }

         return baseValue;
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

      protected override void OnRender(DrawingContext context)
      {
         if (!IsGeometryValid)
         {
            base.OnRender(context);
            var destRect = Rect.Deflate(StrokeThickness);
            context.BeginDraw(this);
            context.DrawEllipse(destRect, Fill, StartAngle, StopAngle, GetPen());
            context.EndDraw(this);
         }
      }
   }
}
