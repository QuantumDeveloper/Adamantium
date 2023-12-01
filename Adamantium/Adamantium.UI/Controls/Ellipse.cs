using System;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public class Ellipse : Shape
{
   public Ellipse()
   {
   }

   public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
      typeof(Double), typeof(Ellipse),
      new PropertyMetadata(0.0,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender,
         StartAngleValueCallback));

   public static readonly AdamantiumProperty StopAngleProperty = AdamantiumProperty.Register(nameof(StopAngle),
      typeof(Double), typeof(Ellipse),
      new PropertyMetadata(360.0,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender,
         StopAngleValueCallback));
   
   public static readonly AdamantiumProperty EllipseTypeProperty = AdamantiumProperty.Register(nameof(EllipseType),
      typeof(EllipseType), typeof(Ellipse),
      new PropertyMetadata(EllipseType.Sector,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender));

   private static object StopAngleValueCallback(AdamantiumComponent adamantiumComponent, object baseValue)
   {
      if (adamantiumComponent is Ellipse ellipse)
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

   private static object StartAngleValueCallback(AdamantiumComponent adamantiumComponent, object baseValue)
   {
      if (adamantiumComponent is Ellipse ellipse)
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
   
   public EllipseType EllipseType
   {
      get => GetValue<EllipseType>(EllipseTypeProperty);
      set => SetValue(EllipseTypeProperty, value);
   }

   protected override void OnRender(DrawingContext context)
   {
      if (IsGeometryValid) return;
      
      var destRect = Rect.Deflate(StrokeThickness);
      context.DrawEllipse(destRect, Fill, StartAngle, StopAngle, EllipseType, GetPen());
   }
}