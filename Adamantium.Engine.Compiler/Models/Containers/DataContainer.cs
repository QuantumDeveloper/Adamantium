using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Compiler.Models.ConversionUtils;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.Containers
{
   public abstract class DataContainer
   {
      protected DataContainer(String filePath)
      {
         FilePath = filePath;
         FileName = Path.GetFileName(filePath);
         Axis = UpAxis.Y_UP_RH;
         UnsupportedFeatures = new List<String>();
      }

      /// <summary>What the file has that the import cannot do. Losing part of a model in silence is the worst
      /// outcome there is: it looks whole right up until somebody notices half of it is missing.</summary>
      public List<String> UnsupportedFeatures { get; }

      public string FilePath { get;}

      public string FileName { get;}

      public ConverterVariant ConverterToUse { get; set; }

      public Boolean IsFileValid { get; set; }

      public abstract FileType Type { get; }

      //Which libraries the file turned out to have
      public Modules Modules { get; internal set; }

      public UpAxis Axis { get; set; }

      public FileMetadata Metadata { get; set; }
      
      public IEnumerable<Modules> SortedModules { get; internal set; }
   }
}
