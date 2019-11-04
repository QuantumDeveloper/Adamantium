using Adamantium.Engine.Effects;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// A collection of <see cref="EffectTechnique"/>.
    /// </summary>
    public sealed class EffectTechniqueCollection:EffectResourcesCollection<EffectTechnique>
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="EffectTechniqueCollection" /> class.
      /// </summary>
      internal EffectTechniqueCollection()
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="EffectTechniqueCollection" /> class.
      /// </summary>
      /// <param name="capacity">The capacity.</param>
      internal EffectTechniqueCollection(int capacity)
         : base(capacity)
      {
      }
   }
}
