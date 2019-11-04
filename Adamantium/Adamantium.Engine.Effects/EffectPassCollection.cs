using Adamantium.Engine.Effects;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// A collection of <see cref="EffectPass"/>.
    /// </summary>
    public sealed class EffectPassCollection:EffectResourcesCollection<EffectPass>
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="EffectPassCollection" /> class.
      /// </summary>
      internal EffectPassCollection()
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="EffectPassCollection" /> class.
      /// </summary>
      /// <param name="capacity">The capacity.</param>
      internal EffectPassCollection(int capacity)
         : base(capacity)
      {
      }
   }
}
