using System;
using System.Collections.Generic;
using Adamantium.Engine.Compiler.Models.ConversionUtils;

namespace Adamantium.Engine.Compiler.Converter.Containers
{
   public class ObjDataContainer : DataContainer
   {
      public ObjDataContainer(String filePath) : base(filePath)
      {
         ConverterToUse = ConverterVariant.ObjConverter;
         Meshes = new Dictionary<String, ObjMeshData>();
         Materials = new Dictionary<String, ObjMaterialData>();
         GeometryData = new ObjGeometryData();
      }

      public ObjGeometryData GeometryData { get; internal set; }

      public Dictionary<String, ObjMeshData> Meshes { get; }

      public Dictionary<String, ObjMaterialData> Materials { get; }

      public override FileType Type => FileType.Obj;
   }
}
