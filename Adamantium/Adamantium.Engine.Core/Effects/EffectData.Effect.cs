using System.Collections.Generic;

namespace Adamantium.Engine.Core.Effects
{
   public sealed partial class EffectData
   {
      public sealed class Effect
      {

         /// <summary>
         /// Name of the effect.
         /// </summary>
         public string Name;

         /// <summary>
         /// Share constant buffers.
         /// </summary>
         public bool ShareConstantBuffers;

         /// <summary>
         /// List of <see cref="Technique"/>.
         /// </summary>
         public List<Technique> Techniques;

         /// <summary>
         /// The compiler arguments used to compile this effect. This field is null if the effect is not compiled with the option "AllowDynamicRecompiling".
         /// </summary>
         public CompilerArguments Arguments;
      }
   }
}
