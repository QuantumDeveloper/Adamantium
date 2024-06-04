namespace Adamantium.Engine.Graphics.Effects
{
   /// <summary>
   /// Type of a resource for an <see cref="EffectParameter" />.
   /// </summary>
   public enum EffectResourceType
   {
      /// <summary>
      /// This is not a resource.
      /// </summary>
      None = 0,

      /// <summary>
      /// A Constant Buffer.
      /// </summary>
      ConstantBuffer = 1,

      /// <summary>
      /// A <see cref="ShaderResourceView"/>.
      /// </summary>
      ShaderResourceView = 2,

      /// <summary>
      /// A <see cref="SamplerState"/>.
      /// </summary>
      SamplerState = 3,

      /// <summary>
      /// An <see cref="UnorderedAccessView"/>.
      /// </summary>
      UnorderedAccessView = 4,
   }
}
