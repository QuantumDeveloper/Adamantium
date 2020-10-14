namespace Adamantium.Engine.Graphics.Effects
{
    /// <summary>
    /// A collection of <see cref="EffectParameter"/>.
    /// </summary>
    public sealed class EffectParameterCollection:NamedObjectsCollection<EffectParameter>
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="EffectParameterCollection" /> class.
      /// </summary>
      internal EffectParameterCollection()
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="EffectParameterCollection" /> class.
      /// </summary>
      /// <param name="capacity">The capacity.</param>
      internal EffectParameterCollection(int capacity)
         : base(capacity)
      {
      }
   }
}
