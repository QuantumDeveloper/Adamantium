using System;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Ellipse:Shape
   {
      private EllipseGeometry geometry;

      public Ellipse()
      {
         geometry = new EllipseGeometry();
      }

      public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
         typeof (Double), typeof (Ellipse),
         new PropertyMetadata((Double) 0,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender, PropertyChangedCallback, StartAngleValueCallback));

      public static readonly AdamantiumProperty StopAngleProperty = AdamantiumProperty.Register(nameof(StopAngle),
         typeof (Double), typeof (Ellipse),
         new PropertyMetadata((Double) 360,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender, PropertyChangedCallback, StopAngleValueCallback));

      private static object StopAngleValueCallback(AdamantiumComponent adamantiumObject, object baseValue)
      {
         var ellipse = adamantiumObject as Ellipse;
         if (ellipse != null)
         {
            var stopAngle = (Double) baseValue;
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
         var ellipse = adamantiumObject as Ellipse;
         if (ellipse != null)
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

      private static void PropertyChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
      {
         
      }


      public Double StartAngle
      {
         get { return GetValue<Double>(StartAngleProperty); }
         set { SetValue(StartAngleProperty, value); }
      }

      public Double StopAngle
      {
         get { return GetValue<Double>(StopAngleProperty); }
         set { SetValue(StopAngleProperty, value); }
      }


      public override Geometry RenderGeometry => geometry;


      public override void OnRender(DrawingContext context)
      {
         if (!IsGeometryValid)
         {
            base.OnRender(context);
            geometry = new EllipseGeometry(new Rect(new Size(ActualWidth, ActualHeight)).Deflate(StrokeThickness), StartAngle, StopAngle);
            context.BeginDraw(this);
            context.DrawGeometry(this, Fill,
               new Pen(Stroke, StrokeThickness, new DashStyle(StrokeDashArray?.AsReadOnly()), StrokeDashCap, StartLineCap,
                  EndLineCap), geometry);
            context.EndDraw(this);
         }
      }

   }
}
