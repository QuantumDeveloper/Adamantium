using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Compiler.Converter.Containers
{
   public class ObjMaterialData
   {
      public ObjMaterialData()
      {
         Data = new List<string>();
      }

      public String Name { get; set; }
      public List<String> Data { get; set; }
   }
}
