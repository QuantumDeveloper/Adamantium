using System;

namespace Adamantium.Engine.Core.Models
{
   /// <summary>
   /// Animation semantic flags
   /// </summary>
   [Flags]
   public enum AnimationSemantic
   {
      /// <summary>
      /// Input semantic
      /// </summary>
      Input = 2,
      /// <summary>
      /// Output semantic
      /// </summary>
      Output = 4,
      /// <summary>
      /// Interpolation semantic
      /// </summary>
      Interpolation = 8
   }
}
