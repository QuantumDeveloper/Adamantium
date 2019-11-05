using System.Collections.Generic;

namespace Adamantium.Engine.Core.Effects
{
   public sealed partial class EffectData
   {
      public sealed class Technique
      {
         /// <summary>
         /// Name of this technique.
         /// </summary>
         /// <remarks>
         /// This value can be null.
         /// </remarks>
         public string Name;

         /// <summary>
         /// List of <see cref="Pass"/>.
         /// </summary>
         public List<Pass> Passes;

         public override string ToString()
         {
            return $"Technique: [{Name}], Passes({Passes.Count})";
         }

         /// <summary>
         /// Clones this instance.
         /// </summary>
         /// <returns>Technique.</returns>
         public Technique Clone()
         {
            var technique = (Technique)MemberwiseClone();
            if (Passes != null)
            {
               technique.Passes = new List<Pass>(Passes.Count);
               for (int i = 0; i < Passes.Count; i++)
               {
                  var pass = Passes[i];
                  technique.Passes.Add(pass?.Clone());
               }
            }
            return technique;
         }
      }
   }
}
