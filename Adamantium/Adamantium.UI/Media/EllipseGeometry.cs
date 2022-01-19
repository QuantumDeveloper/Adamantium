using System;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI.Media;

public sealed class EllipseGeometry : Geometry
{
   private Rect bounds;
      
   public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
      typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0, PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
      typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0, PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty CenterProperty = AdamantiumProperty.Register(nameof(Center),
      typeof(Vector2), typeof(EllipseGeometry), new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
      typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)0, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StopAngleProperty = AdamantiumProperty.Register(nameof(StopAngle),
      typeof(Double), typeof(EllipseGeometry), new PropertyMetadata((Double)360, PropertyMetadataOptions.AffectsRender));


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

   public Vector2 Center
   {
      get => GetValue<Vector2>(CenterProperty);
      set => SetValue(CenterProperty, value);
   }

   public EllipseGeometry()
   {
      IsClosed = true;
      TesselationFactor = 80;
   }

   public EllipseGeometry(Rect rect, Double startAngle = 0, Double stopAngle = 360) : this()
   {
      bounds = rect;
      RadiusX = rect.Width/2;
      RadiusY = rect.Height/2;
      Center = rect.Center;
      StartAngle = startAngle;
      StopAngle = stopAngle;
      IsClosed = true;
   }

   public EllipseGeometry(Vector2 center, Double radiusX, Double radiusY, Double startAngle = 0, Double stopAngle = 360) : this()
   {
      bounds = new Rect(center - new Vector2(radiusX, radiusY), new Size(radiusX * 2, radiusY * 2));
      Center = center;
      RadiusX = radiusX;
      RadiusY = radiusY;
      StartAngle = startAngle;
      StopAngle = stopAngle;
   }

   private void CreateEllipse(Rect rect, Double startAngle = 0, Double stopAngle = 360)
   {
      bounds = rect;
      RadiusX = rect.Width / 2;
      RadiusY = rect.Height / 2;
      Center = rect.Center;
      StartAngle = startAngle;
      StopAngle = stopAngle;
         
      var translation = Matrix4x4.Translation(Center.X, Center.Y, 0);
         
      Mesh = Shapes.Ellipse.GenerateGeometry(
         GeometryType.Solid, 
         EllipseType.Sector,
         new Vector2(rect.Width, rect.Height), 
         StartAngle, 
         StopAngle,
         tessellation: TesselationFactor,
         transform: translation);
         
      OutlineMesh = Shapes.Ellipse.GenerateGeometry(
         GeometryType.Outlined, 
         EllipseType.Sector,
         new Vector2((float)rect.Width, (float)rect.Height), 
         (float)StartAngle, 
         (float)StopAngle,
         tessellation: TesselationFactor,
         transform: translation);
   }

   public override Rect Bounds => bounds;

   public override Geometry Clone()
   {
      return null;
   }

   public override void RecalculateBounds()
   {
      bounds = Rect.FromPoints(OutlineMesh.Points);
   }

   protected internal override void ProcessGeometryCore()
   {
      CreateEllipse(Bounds, StartAngle, StopAngle);
   }
}