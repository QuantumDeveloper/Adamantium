using System;

namespace Adamantium.Engine.Compiler.Converter
{
   public class FileMetadata
   {
      public string Version { get; set; }
      public string AuthoringTool { get; set; }
      public string Author { get; set; }
      public DateTime CreationDate { get; set; }
      public DateTime ModificationDate { get; set; }

   }
}
