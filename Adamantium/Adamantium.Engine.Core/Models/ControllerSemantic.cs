using System;

namespace Adamantium.Engine.Core.Models
{
   [Flags]
   public enum ControllerSemantic
   {
      Joint = 0,
      Weight = 1,
      InverseBindMatrix = 2
   }
}
