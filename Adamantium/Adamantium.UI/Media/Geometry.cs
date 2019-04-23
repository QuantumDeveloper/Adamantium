using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public abstract class Geometry:DependencyComponent
   {
      internal List<VertexPositionTexture> VertexArray { get; set; }
      internal List<Int32> IndicesArray { get; set; }
      public Matrix4x4F Transform { get; set; }
      protected int lastIndex = 0;
      protected readonly int interrupt = -1;

      public bool IsOptimized { get; set; }

      internal Geometry()
      {
         PrimitiveType = PrimitiveType.TriangleStrip;
         Transformation = Matrix4x4F.Identity;
         VertexArray = new List<VertexPositionTexture>();
         IndicesArray = new List<int>();
      }
      public PrimitiveType PrimitiveType { get; protected set; }
      
      public abstract Rect Bounds { get; }
      public Matrix4x4F Transformation { get; set; }

      protected virtual Int32[] OptimizeShape(VertexPositionTexture[] geometry, out VertexPositionTexture[] newGeometry)
      {
         List<Int32> indices = new List<int>();
         Dictionary<VertexPositionTexture, Int32> dict = new Dictionary<VertexPositionTexture, Int32>();
         List<VertexPositionTexture> tmp = new List<VertexPositionTexture>();

         int decrement = 0;
         //переменная decrement нужна для того, чтобы хранить сколько раз уже встречались одни и те же значения в вершинном буфере
         //В итоге при записи в индексный массив это значение отнимается от текущего индекса цикла.
         //Таким образом максимльное значение в индексном массиве не будет превышать количество вершин в вершинном массиве
         for (int i = 0; i < geometry.Length; i++)
         {
            if (dict.ContainsKey(geometry[i]))
            {
               indices.Add(dict[geometry[i]]);
               decrement++;
            }
            else
            {
               dict.Add(geometry[i], i - decrement);
               tmp.Add(geometry[i]);
               indices.Add(i - decrement);
            }
         }
         newGeometry = tmp.ToArray();

         return indices.ToArray();
      }

      public abstract Geometry Clone();

      public Boolean IsEmpty()
      {
         if (VertexArray.Count == 0)
         {
            return true;
         }
         return false;
      }
   }
}
