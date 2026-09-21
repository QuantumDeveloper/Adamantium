using System;

namespace Adamantium.Engine.Compiler.Models.ConversionUtils
{
   //Where each semantic sits within one vertex of the index stream
   public class Offset
   {
      public ulong? Position { get; set; }
      public ulong? Normal { get; set; }
      public ulong? UV0 { get; set; }
      public ulong? UV1 { get; set; }
      public ulong? UV2 { get; set; }
      public ulong? UV3 { get; set; }
      public ulong? Color { get; set; }

      /// <summary>The highest offset of ANY input, including the ones we do not read. The stride is that plus one, so
      /// leaving out a TANGENT or TEXBINORMAL - which Blender and Maya both write - made the stride too small and
      /// shifted every index after it.</summary>
      public void Observe(ulong offset)
      {
         if (offset > highest) highest = offset;
      }

      private ulong highest;

      public int CommonOffset
      {
         get
         {
            int offset = (int)highest;
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
