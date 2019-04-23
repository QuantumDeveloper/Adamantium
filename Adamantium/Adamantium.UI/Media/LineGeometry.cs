using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public class LineGeometry:Geometry
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
         PrimitiveType = PrimitiveType.TriangleStrip;
         VertexArray = new List<VertexPositionTexture>();
         IndicesArray = new List<int>();
      }

      public LineGeometry(Point startPoint, Point endPoint, Double thickness)
      {
         PrimitiveType = PrimitiveType.TriangleStrip;
         VertexArray = new List<VertexPositionTexture>();
         IndicesArray = new List<int>();
         StartPosition = startPoint;
         EndPosition = endPoint;

         CreateLine(thickness);
      }

      internal void CreateLine(Double thickness)
      {
         var point2 = EndPosition - StartPosition;
         var cross = new Vector2F((float)point2.Y, (float)-point2.X) * -1;
         cross.Normalize();
         var p0 = StartPosition + cross * (float)thickness;
         var p1 = EndPosition + cross * (float)thickness;

         VertexArray.Add(new VertexPositionTexture(StartPosition, Vector2F.Zero));
         VertexArray.Add(new VertexPositionTexture(EndPosition, new Vector2F(1, 0)));
         VertexArray.Add(new VertexPositionTexture(new Vector3F(p0, 0), Vector2F.One));
         VertexArray.Add(new VertexPositionTexture(new Vector3F(p1, 0), new Vector2F(0, 1)));

         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(lastIndex++);
         IndicesArray.Add(interrupt);
      }
   }
}
