using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class LineGeometry : Geometry
   {
      public override Rect Bounds { get; }

      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }

      public Vector2 StartPosition { get; set; }

      public Vector2 EndPosition { get; set; }
      
      public Double Thickness { get; set; }

      public LineGeometry()
      {
      }

      public LineGeometry(Vector2 startPoint, Vector2 endPoint, Double thickness)
      {
         StartPosition = startPoint;
         EndPosition = endPoint;
         Thickness = thickness;
      }
      
      protected internal override void ProcessGeometry()
      {
         CreateLine(Thickness);
      }

      internal void CreateLine(Double thickness)
      {
         Mesh = Engine.Graphics.Shapes.Line.GenerateGeometry(
            GeometryType.Solid, 
            (Vector3F)StartPosition,
            (Vector3F)EndPosition,
            (float)thickness);
      }
   }
}
