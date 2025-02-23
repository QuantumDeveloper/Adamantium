using System;

namespace Adamantium.Graphics.Core.Models
{
   [Flags]
   public enum ControllerSemantic
   {
      Joint = 0,
      Weight = 1,
      InverseBindMatrix = 2
   }
}
