using Adamantium.Core;

namespace Adamantium.Engine.Graphics.Effects
{
   /// <summary>
   /// Represents an effect technique. 
   /// </summary>
   public sealed class EffectTechnique:NamedObject
   {
      private readonly Effect effect;

      internal EffectTechnique(Effect effect, string name)
      {
         Name = name;
         this.effect = effect;
         Passes = new EffectPassCollection();
      }

      /// <summary>
      /// Gets the collection of EffectPass objects this rendering technique requires.
      /// </summary>
      public EffectPassCollection Passes { get; private set; }
   }
}
