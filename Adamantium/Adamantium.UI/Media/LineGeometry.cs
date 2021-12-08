using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Media
{
   public class LineGeometry : Geometry
   {
      private Rect bounds;

      public static readonly AdamantiumProperty StartPointProperty =
         AdamantiumProperty.Register(nameof(StartPoint), typeof(Vector2), typeof(LineGeometry),
            new PropertyMetadata(new Vector2(0), PropertyMetadataOptions.AffectsMeasure));
      
      public static readonly AdamantiumProperty EndPointProperty =
         AdamantiumProperty.Register(nameof(EndPoint), typeof(Vector2), typeof(LineGeometry),
            new PropertyMetadata(new Vector2(0), PropertyMetadataOptions.AffectsMeasure));
      
      public static readonly AdamantiumProperty ThicknessProperty =
               AdamantiumProperty.Register(nameof(Thickness), typeof(Double), typeof(LineGeometry),
                  new PropertyMetadata(0d, PropertyMetadataOptions.AffectsMeasure));

      public override Rect Bounds => bounds;

      public Vector2 StartPoint
      {
         get => GetValue<Vector2>(StartPointProperty);
         set => SetValue(StartPointProperty, value);
      }

      public Vector2 EndPoint
      {
         get => GetValue<Vector2>(EndPointProperty);
         set => SetValue(EndPointProperty, value);
      }

      public Double Thickness
      {
         get => GetValue<Double>(ThicknessProperty);
         set => SetValue(ThicknessProperty, value);
      }

      public LineGeometry()
      {
      }

      public LineGeometry(Vector2 startPoint, Vector2 endPoint, Double thickness) : this()
      {
         StartPoint = startPoint;
         EndPoint = endPoint;
         Thickness = thickness;
         bounds = new Rect(startPoint, endPoint);
      }

      public override void RecalculateBounds()
      {
         bounds = new Rect(StartPoint, EndPoint);
      }
      
      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }

      protected internal override void ProcessGeometry()
      {
         CreateLine(Thickness);
      }

      internal void CreateLine(Double thickness)
      {
         Mesh = Engine.Graphics.Shapes.Line.GenerateGeometry(
            GeometryType.Solid, 
            (Vector3F)StartPoint,
            (Vector3F)EndPoint,
            (float)thickness);
      }
   }
}
