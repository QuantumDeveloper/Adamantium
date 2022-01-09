using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Compiler.Converter.Parsers;
using Adamantium.Engine.Compiler.Models.ConversionUtils;

namespace Adamantium.Engine.Compiler.Converter.Converters
{
   public class ObjConverter: ConverterBase
   {
      private ObjDataContainer dataContainer;
      private ObjConversionExecutor executor;
      public ObjConverter(string filePath, ConversionConfig config) :base(filePath, config)
      {
         Parser = new ObjFileParser(filePath);
         executor = new ObjConversionExecutor(Config, UpAxis.Y_UP_RH);
      }

      protected override void Convert()
      {
         dataContainer = (ObjDataContainer) Parser.ParseDataAsync(Config).Result;
         if (!dataContainer.IsFileValid)
         {
            IsCancelled = true;
            return;
         }

         SceneDataContainer.Models = SceneDataContainer.CreateMesh(null, "", Path.GetFileNameWithoutExtension(dataContainer.FileName));
         foreach (var mesh in dataContainer.Meshes)
         {
            ParseData(mesh.Value);
         }

         foreach (var material in dataContainer.Materials)
         {
            ParseData(material.Value);
         }
      }

      private void ParseData(ObjMeshData meshData)
      {
         List<IndicesContainer> indicesContainers = new List<IndicesContainer>();
         //Получаем индексы вершин
         foreach (var indices in meshData.GeometrySemantic)
         {
            indicesContainers.Add(executor.DistributeIndices(indices));
         }

         executor.ConstructMeshGeometry(SceneDataContainer, indicesContainers, dataContainer.GeometryData, meshData);
      }


      private void ParseData(ObjMaterialData materialData)
      {
         SceneData.Material material = executor.GetMaterial(materialData);
         lock (SceneDataContainer.Materials)
         {
            SceneDataContainer.Materials.Add(material.ID, material);
         }

         GetImages(material);
      }

      private void GetImages(SceneData.Material material)
      {
         var directory = Path.GetDirectoryName(dataContainer.FilePath);
         lock (SceneDataContainer.Images)
         {
            if (!String.IsNullOrEmpty(material.AmbientMap) &&
                !SceneDataContainer.Images.ContainsKey(material.AmbientMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.AmbientMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
            if (!String.IsNullOrEmpty(material.AlphaTextureMap) &&
                !SceneDataContainer.Images.ContainsKey(material.AlphaTextureMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.AlphaTextureMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
            if (!String.IsNullOrEmpty(material.DiffuseMap) &&
                !SceneDataContainer.Images.ContainsKey(material.DiffuseMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.DiffuseMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
            if (!String.IsNullOrEmpty(material.SpecularColorMap) &&
                !SceneDataContainer.Images.ContainsKey(material.SpecularColorMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.SpecularColorMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
            if (!String.IsNullOrEmpty(material.SpecularHighlightMap) &&
                !SceneDataContainer.Images.ContainsKey(material.SpecularHighlightMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.SpecularHighlightMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
            if (!String.IsNullOrEmpty(material.DisplacementMap) &&
                !SceneDataContainer.Images.ContainsKey(material.DisplacementMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.DisplacementMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
            if (!String.IsNullOrEmpty(material.BumpMap) &&
                !SceneDataContainer.Images.ContainsKey(material.BumpMap))
            {
               var img = new SceneData.Image();
               img.ImageName = material.BumpMap;
               img.FilePath = Path.Combine(directory, img.ImageName);
               SceneDataContainer.Images.Add(img.ImageName, img);
            }
         }
      }
   }
}
