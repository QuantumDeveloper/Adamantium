namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Represents dimension of the texture 1D, 2D, 3D or Cube
   /// </summary>
   public enum TextureDimension
   {
      /// <summary>
      /// The texture dimension is unknown.
      /// </summary>
      Undefined,
      
      /// <summary>
      /// The texture dimension is 1D.
      /// </summary>
      Texture1D,

      /// <summary>
      /// The texture dimension is 2D.
      /// </summary>
      Texture2D,

      /// <summary>
      /// The texture dimension is 3D.
      /// </summary>
      Texture3D,

      /// <summary>
      /// The texture dimension is a CubeMap.
      /// </summary>
      TextureCube,
   }
}
