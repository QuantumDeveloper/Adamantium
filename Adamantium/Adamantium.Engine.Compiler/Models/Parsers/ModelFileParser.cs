using System;
using System.Threading.Tasks;
using Adamantium.Engine.Compiler.Converter.Configs;
using Adamantium.Engine.Compiler.Converter.Containers;

namespace Adamantium.Engine.Compiler.Converter.Parsers
{
   public abstract class ModelFileParser
   {
      protected String FilePath { get; }

      public Modules Modules { get; internal set; }

      public ModelFileParser(string filePath)
      {
         FilePath = filePath;
      }

      public Task<DataContainer> ParseDataAsync(ConversionConfig config)
      {
         return Task.Run(() => ParseData(config));
      }

      protected abstract DataContainer ParseData(ConversionConfig config);
   }
}
