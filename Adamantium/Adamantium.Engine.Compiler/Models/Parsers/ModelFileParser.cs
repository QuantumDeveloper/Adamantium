using System;
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

      /// <summary>The parse is synchronous. The Task.Run wrapper was here only for all three callers to block on
      /// .Result: the work was awaited on the spot anyway, and the price was a thread hop plus an
      /// AggregateException in place of the real exception.</summary>
      public abstract DataContainer ParseData(ConversionConfig config);
   }
}
