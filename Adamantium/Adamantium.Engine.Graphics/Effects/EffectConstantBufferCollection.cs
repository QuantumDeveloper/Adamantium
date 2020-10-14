namespace Adamantium.Engine.Graphics.Effects
{
    /// <summary>
    /// A collection of <see cref="EffectConstantBuffer"/>.
    /// </summary>
    public sealed class EffectConstantBufferCollection:NamedObjectsCollection<EffectConstantBuffer>
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="EffectConstantBufferCollection" /> class.
      /// </summary>
      internal EffectConstantBufferCollection()
      {
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="EffectConstantBufferCollection" /> class.
      /// </summary>
      /// <param name="capacity">The capacity.</param>
      internal EffectConstantBufferCollection(int capacity)
         : base(capacity)
      {
      }
   }
}
