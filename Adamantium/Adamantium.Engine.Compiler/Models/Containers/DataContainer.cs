using System;
using System.Collections.Generic;
using System.IO;
using Adamantium.Engine.Compiler.Models.ConversionUtils;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.Containers
{
   public abstract class DataContainer
   {
      protected DataContainer(String filePath)
      {
         FilePath = filePath;
         FileName = Path.GetFileName(filePath);
         Axis = UpAxis.Y_UP_RH;
      }

      //путь к файлу
      public string FilePath { get;}
      //имя файла
      public string FileName { get;}
      //какой конвертер использовать
      public ConverterVariant ConverterToUse { get; set; }
      //переменая содержит значение является ли файл валидным
      public Boolean IsFileValid { get; set; }
      //тип файла
      public abstract FileType Type { get; }

      //коллекция доступных в файле библиотек
      public Modules Modules { get; internal set; }

      //Ориентация осей
      public UpAxis Axis { get; set; }

      public FileMetadata Metadata { get; set; }
      
      public IEnumerable<Modules> SortedModules { get; internal set; }
   }
}
