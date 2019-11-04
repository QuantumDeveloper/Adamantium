using System.Collections.Generic;
using ProtoBuf;

namespace Adamantium.Engine.Core.Effects
{
   public sealed partial class EffectData
   {
      [ProtoContract]
      public sealed class Effect
      {

         /// <summary>
         /// Name of the effect.
         /// </summary>
         [ProtoMember(1)]
         public string Name;

         /// <summary>
         /// Share constant buffers.
         /// </summary>
         [ProtoMember(2)]
         public bool ShareConstantBuffers;

         /// <summary>
         /// List of <see cref="Technique"/>.
         /// </summary>
         [ProtoMember(3)]
         public List<Technique> Techniques;

         /// <summary>
         /// The compiler arguments used to compile this effect. This field is null if the effect is not compiled with the option "AllowDynamicRecompiling".
         /// </summary>
         [ProtoMember(4)]
         public CompilerArguments Arguments;
      }
   }
}
