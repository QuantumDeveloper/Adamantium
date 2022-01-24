using System;
using System.Collections.Generic;

namespace Adamantium.UI.Media;

public sealed class StreamGeometry : Geometry
{
   private Rect bounds;
   private StreamGeometryContext context;

   public StreamGeometry()
   {
      context = new StreamGeometryContext();
   }

   public override Rect Bounds => bounds;

   public StreamGeometryContext Open()
   {
      context = new StreamGeometryContext();
      return context;
   }
   
   public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
      typeof(FillRule), typeof(StreamGeometry),
      new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));
   
   public FillRule FillRule
   {
      get => GetValue<FillRule>(FillRuleProperty);
      set => SetValue(FillRuleProperty, value);
   }

   public override Geometry Clone()
   {
      throw new NotImplementedException();
   }

   public override void RecalculateBounds()
   {
      var contours = context.GetContours();
      var points = new List<Vector2>();
      foreach (var contour in contours)
      {
         points.AddRange(contour.Points);
      }
      bounds = Rect.FromPoints(points);
   }

   protected internal override void ProcessGeometryCore()
   {
      context.ProcessFigures();
      var contours = context.GetContours();
      Mesh.Clear();
      Mesh.AddContours(contours);
      var polygon = new Polygon();
      polygon.FillRule = FillRule;
      polygon.AddItems(contours);

      var points = polygon.Fill();
      Mesh.SetPoints(points);
   }
}