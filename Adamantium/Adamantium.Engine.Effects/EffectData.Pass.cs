﻿
using MessagePack;

namespace Adamantium.Engine.Effects
{
   public sealed partial class EffectData
   {
      [MessagePackObject]
      public sealed class Pass
      {
         private EffectPropertyCollection properties;

         /// <summary>
         /// Name of this pass.
         /// </summary>
         [Key(0)]
         public string Name;

         /// <summary>
         /// True if this pass is the sub-pass of a root pass.
         /// </summary>
         [Key(1)]
         public bool IsSubPass;

         /// <summary>
         /// List of <see cref="EffectData.Properties"/>.
         /// </summary>
         [Key(2)]
         public EffectPropertyCollection Properties
         {
            get { return properties ??= new EffectPropertyCollection(); }
            set => properties = value;
         }

         /// <summary>
         /// Description of the shader stage <see cref="Pipeline"/>.
         /// </summary>
         [Key(3)]
         public Pipeline Pipeline;

         /// <summary>
         /// Clones this instance.
         /// </summary>
         /// <returns>Pass.</returns>
         public Pass Clone()
         {
            var pass = (Pass)MemberwiseClone();
            if (pass.Properties != null)
            {
               pass.Properties = pass.Properties.Clone();
            }
            if (pass.Pipeline != null)
            {
               pass.Pipeline = pass.Pipeline.Clone();
            }

            return pass;
         }

         /// <summary>
         /// Returns a string that represents the current object.
         /// </summary>
         /// <returns>
         /// A string that represents the current object.
         /// </returns>
         /// <filterpriority>2</filterpriority>
         public override string ToString()
         {
            return $"Pass: [{Name}], SubPass: {IsSubPass}, Attributes({Properties.Count})";
         }
      }
   }
}
