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

      public Point StartPosition { get; set; }

      public Point EndPosition { get; set; }

      public LineGeometry()
      {
      }

      public LineGeometry(Point startPoint, Point endPoint, Double thickness)
      {
         StartPosition = startPoint;
         EndPosition = endPoint;

         CreateLine(thickness);
      }

      internal void CreateLine(Double thickness)
      {
         Mesh = Engine.Graphics.Shapes.Line.GenerateGeometry(
            GeometryType.Solid, 
            StartPosition,
            EndPosition,
            (float)thickness);
      }
   }
}
