using System;
using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Mathematics;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public class Border : Decorator
{
   public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
      typeof (Brush), typeof (Border),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
      typeof (CornerRadius), typeof (Border),
      new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty BorderThicknessProperty =
      AdamantiumProperty.Register(nameof(BorderThickness),
         typeof (Thickness), typeof (Border),
         new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
      typeof (Brush), typeof (Border),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public Brush BorderBrush
   {
      get => GetValue<Brush>(BorderBrushProperty);
      set => SetValue(BorderBrushProperty, value);
   }

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }

   public CornerRadius CornerRadius
   {
      get => GetValue<CornerRadius>(CornerRadiusProperty);
      set => SetValue(CornerRadiusProperty, value);
   }

   public Thickness BorderThickness
   {
      get => GetValue<Thickness>(BorderThicknessProperty);
      set => SetValue(BorderThicknessProperty, value);
   }

   public Border()
   {
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      var child = Child;
      var padding = Padding + BorderThickness;
      var size = availableSize.Deflate(padding);
      if (child != null)
      {
         child.Measure(size);
         return child.DesiredSize.Inflate(padding);
      }
      else
      {
         return new Size(padding.Left + padding.Right, padding.Bottom + padding.Top);
      }
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      if (Child != null)
      {
         var padding = Padding + BorderThickness;
         Child.Arrange(new Rect(finalSize).Deflate(padding));
      }
      return finalSize;
   }

   protected override void OnRender(DrawingContext context)
   {
         
      var size = new Size(ActualWidth, ActualHeight);
      var borderThickness = BorderThickness;
      var cornerRadius = CornerRadius;
      base.OnRender(context);

      var commonTimer = Stopwatch.StartNew();
      var outerGeometry = new RectangleGeometry(new Rect(size), cornerRadius);
      outerGeometry.ProcessGeometry();
      var innerSize = size.Deflate(BorderThickness);
      var innerRect = new Rect(new Vector2(borderThickness.Left, borderThickness.Top), innerSize);
      var innerGeometry = new RectangleGeometry(innerRect, cornerRadius);
      innerGeometry.ProcessGeometry();
      var outerPolygonItem = new PolygonItem(outerGeometry.OutlineMesh.Points);
      var innerPolygonItem = new PolygonItem(innerGeometry.OutlineMesh.Points);
      var poly = new Mathematics.Polygon();
      poly.AddItems(outerPolygonItem, innerPolygonItem);
      poly.FillRule = FillRule.EvenOdd;
      var points = poly.Fill();

      var polygonGeometry = new PolygonGeometry();
      polygonGeometry.FillRule = FillRule.EvenOdd;
      polygonGeometry.Points = new PointsCollection();
      foreach (var point in points)
      {
         polygonGeometry.Points.Add(Vector2.Round(point.X, point.Y, 3));
      }
         
      context.BeginDraw(this);
      //context.DrawRectangle(Background, innerRect, CornerRadius);
      var timer = Stopwatch.StartNew();
      //context.DrawGeometry(BorderBrush, polygonGeometry);
      timer.Stop();
      commonTimer.Stop();
      Console.WriteLine($"Triangulation time: {timer.ElapsedMilliseconds}");
      Console.WriteLine($"Common time: {commonTimer.ElapsedMilliseconds}");
      context.EndDraw(this);
         
   }
}