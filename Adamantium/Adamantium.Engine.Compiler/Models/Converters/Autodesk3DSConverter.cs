using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;
using Adamantium.Engine.Compiler.Converter.ConversionUtils;
using Adamantium.Engine.Compiler.Converter.Parsers;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.Converters
{
   public class Autodesk3DSConverter : ConverterBase
   {
      public Autodesk3DSConverter(string filePath, ConversionConfig config) : base(filePath, config, UpAxis.Z_UP)
      {
      }

      private Autodesk3DSConversionExecutor executor;

      protected override void Convert()
      {
         Parser = new Autodesk3DSFileParser(FilePath);
         executor = new Autodesk3DSConversionExecutor(Config, UpAxis.Z_UP);
         var data =  (Autodesk3DsDataContainer)Parser.ParseDataAsync(Config).Result;
         SceneDataContainer = data.Data;

         UpdateImagePathes();
      }

      private void UpdateImagePathes()
      {
         foreach (var image in SceneDataContainer.Images.Values)
         {
            image.FilePath = Path.Combine(Path.GetDirectoryName(FilePath), image.ImageName);
         }
      }
   }
}
