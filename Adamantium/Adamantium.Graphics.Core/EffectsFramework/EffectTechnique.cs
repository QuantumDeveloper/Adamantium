using Adamantium.Core;

namespace Adamantium.Graphics.Core.EffectsFramework;

/// <summary>
/// Represents an effect technique. 
/// </summary>
public sealed class EffectTechnique:NamedObject
{
   internal EffectTechnique(string name)
   {
      Name = name;
      Passes = new EffectPassCollection();
   }

   /// <summary>
   /// Gets the collection of EffectPass objects this rendering technique requires.
   /// </summary>
   public EffectPassCollection Passes { get; private set; }
}