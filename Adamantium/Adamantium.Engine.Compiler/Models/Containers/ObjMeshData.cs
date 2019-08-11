using System;
using System.Collections.Generic;
using Adamantium.Engine.Compiler.Converter.ConversionUtils;

namespace Adamantium.Engine.Compiler.Converter.Containers
{
   public class ObjMeshData
   {
      public ObjMeshData(ObjectType type)
      {
         Type = type;
         GeometrySemantic = new List<RawIndicesSemanticData>();
      }

      public String Name { get; set; }
      public ObjectType Type { get; }
      public List<RawIndicesSemanticData> GeometrySemantic { get; set; } 
   }
}
