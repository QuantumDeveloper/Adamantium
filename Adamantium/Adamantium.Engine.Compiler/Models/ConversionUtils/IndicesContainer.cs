using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.ConversionUtils
{
   //Класс используется для хранения индексов строк вершин, нормалей и текстурных координат 
   //для использования их при сборке геометрии в правильном порядке
   internal class IndicesContainer
   {
      public IndicesContainer()
      {
         Positions = new List<int>();
         UV0 = new List<int>();
         UV1 = new List<int>();
         UV2 = new List<int>();
         UV3 = new List<int>();
         Colors = new List<int>();
      }

      public List<int> Positions { get; set; }

      public List<int> UV0 { get; set; }

      public List<int> UV1 { get; set; }

      public List<int> UV2 { get; set; }

      public List<int> UV3 { get; set; }

      public List<int> Colors { get; set; }

      public VertexSemantic Semantic { get; set; }

      public String MaterialId { get; set; }

      public PrimitiveType MeshTopology { get; set; }

   }
}
