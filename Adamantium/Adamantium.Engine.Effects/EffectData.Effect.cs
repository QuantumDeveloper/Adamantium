using System.Collections.Generic;
using MessagePack;

namespace Adamantium.Engine.Effects
{
   public sealed partial class EffectData
   {
      [MessagePackObject]
      public sealed class Effect
      {

         /// <summary>
         /// Name of the effect.
         /// </summary>
         [Key(0)]
         public string Name;

         /// <summary>
         /// Share constant buffers.
         /// </summary>
         [Key(1)]
         public bool ShareConstantBuffers;

         /// <summary>
         /// List of <see cref="Technique"/>.
         /// </summary>
         [Key(2)]
         public List<Technique> Techniques;

         /// <summary>
         /// The compiler arguments used to compile this effect. This field is null if the effect is not compiled with the option "AllowDynamicRecompiling".
         /// </summary>
         [Key(3)]
         public CompilerArguments Arguments;
      }
   }
}
