using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

public class Ellipse : Shape
{
   static Ellipse()
   {
      // Box-shape default (as in WPF): fill the layout slot. Path/Polygon/Line keep the base Stretch.None.
      StretchProperty.OverrideMetadata(typeof(Ellipse), new PropertyMetadata(Stretch.Fill,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));
   }

   public Ellipse()
   {
   }

   public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
      typeof(Double), typeof(Ellipse),
      new PropertyMetadata(0.0,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender,
         StartAngleValueCallback));

   public static readonly AdamantiumProperty SweepAngleProperty = AdamantiumProperty.Register(nameof(StopAngle),
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
      get => GetValue<Double>(SweepAngleProperty);
      set => SetValue(SweepAngleProperty, value);
   }
   
   public EllipseType EllipseType
   {
      get => GetValue<EllipseType>(EllipseTypeProperty);
      set => SetValue(EllipseTypeProperty, value);
   }

   protected override void OnRender(IDrawingContext context)
   {
      if (IsGeometryValid) return;
      
      var destRect = Rect.Deflate(StrokeThickness / 2);
      context.ForControl(this).DrawEllipse(destRect, Fill, StartAngle, StopAngle, EllipseType, GetPen());
   }

   // Tight hit test: inside the ellipse (the bounding-box corners are excluded - the main false positive), and for an
   // unfilled ellipse only the stroke ring, not the hollow centre.
   public override bool HitTestCore(Vector2 localPoint)
   {
      double rx = Rect.Width / 2.0, ry = Rect.Height / 2.0;
      if (rx <= 0 || ry <= 0) return false;
      double cx = Rect.X + rx, cy = Rect.Y + ry;

      double ndx = (localPoint.X - cx) / rx, ndy = (localPoint.Y - cy) / ry;
      if (ndx * ndx + ndy * ndy > 1.0) return false;          // outside the ellipse
      if (IsVisibleBrush(Fill)) return true;                  // filled: anywhere inside

      double irx = Math.Max(rx - StrokeThickness, 0.01), iry = Math.Max(ry - StrokeThickness, 0.01);
      double idx = (localPoint.X - cx) / irx, idy = (localPoint.Y - cy) / iry;
      return idx * idx + idy * idy >= 1.0;                    // stroke-only: just the ring
   }
}