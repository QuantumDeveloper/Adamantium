using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public class LineGeometry : Geometry
   {
      public override Rect Bounds { get; }

      public override Geometry Clone()
      {
         throw new NotImplementedException();
      }

      public Vector2D StartPosition { get; set; }

      public Vector2D EndPosition { get; set; }

      public LineGeometry()
      {
      }

      public LineGeometry(Vector2D startPoint, Vector2D endPoint, Double thickness)
      {
         StartPosition = startPoint;
         EndPosition = endPoint;

         CreateLine(thickness);
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
