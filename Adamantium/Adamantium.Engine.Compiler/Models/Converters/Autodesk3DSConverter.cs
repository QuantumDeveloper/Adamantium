using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;
using Adamantium.Engine.Compiler.Converter.Parsers;
using Adamantium.Engine.Compiler.Models.ConversionUtils;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.Converters
{
   public class Autodesk3DSConverter : ConverterBase
   {
      public Autodesk3DSConverter(string filePath, ConversionConfig config) : base(filePath, config, UpAxis.Z_UP)
      {
      }

      protected override void Convert()
      {
         Parser = new Autodesk3DSFileParser(FilePath);
         var data =  (Autodesk3DsDataContainer)Parser.ParseData(Config);

         // The parser builds its own SceneData, so taking it wholesale dropped the name the base class had put on
         // the container - and the cooked artifact came out nameless.
         SceneDataContainer = data.Data;
         SceneDataContainer.Name = FileName;

         UpdateImagePathes();
         UnsupportedFeatures.AddRange(data.UnsupportedFeatures);
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
