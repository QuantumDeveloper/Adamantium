using System;

namespace Adamantium.Engine.Compiler.Models.ConversionUtils
{
   //Класс для хранения данных оффсета геометрии
   public class Offset
   {
      public ulong? Position { get; set; }
      public ulong? Normal { get; set; }
      public ulong? UV0 { get; set; }
      public ulong? UV1 { get; set; }
      public ulong? UV2 { get; set; }
      public ulong? UV3 { get; set; }
      public ulong? Color { get; set; }

      public int CommonOffset
      {
         get
         {
            int offset = -1;
            if (Position != null)
            {
               offset = Math.Max((int) Position, offset);
            }
            if (Normal != null)
            {
               offset = Math.Max((int)Normal, offset);
            }
            if (UV0 != null)
            {
               offset = Math.Max((int)UV0, offset);
            }
            if (UV1 != null)
            {
               offset = Math.Max((int)UV1, offset);
            }
            if (UV2 != null)
            {
               offset = Math.Max((int)UV2, offset);
            }
            if (UV3 != null)
            {
               offset = Math.Max((int)UV3, offset);
            }
            if (Color != null)
            {
               offset = Math.Max((int)Color, offset);
            }
            return offset;
         }
      }
   }
}
