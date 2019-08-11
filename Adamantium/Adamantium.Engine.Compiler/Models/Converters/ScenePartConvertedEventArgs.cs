using System;

namespace Adamantium.Engine.Compiler.Converter.Converters
{
   public class ScenePartConvertedEventArgs :EventArgs
   {
      public String FileName { get; set; }
      public Int32 CurrentPartNumber { get; set; }

      public Int32 WholePartsCount { get; set; }
      public ScenePartConvertedEventArgs(String fileName, Int32 currentPartNumber, Int32 wholePartsCount)
      {
         FileName = fileName;
         CurrentPartNumber = currentPartNumber;
         WholePartsCount = wholePartsCount;
      }
   }
}
