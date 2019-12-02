namespace Adamantium.Engine.Graphics.Effects
{
    /// <summary>
    /// A collection of <see cref="EffectTechnique"/>.
    /// </summary>
    public sealed class EffectTechniqueCollection:NamedObjectsCollection<EffectTechnique>
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
