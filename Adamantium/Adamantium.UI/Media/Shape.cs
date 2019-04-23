using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI.Media
{
   public class Shape
   {
      public PrimitiveType PrimitiveType { get; internal set; }

      public List<VertexPositionTexture> VertexArray { get; set; }

      public List<Int32> IndexArray { get; set; }

      public bool IsOptimized { get; set; }

      public Shape()
      {
         VertexArray = new List<VertexPositionTexture>();
         IndexArray = new List<int>();
      }

   }
}
