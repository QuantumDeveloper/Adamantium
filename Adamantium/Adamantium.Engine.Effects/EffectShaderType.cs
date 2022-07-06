namespace Adamantium.Engine.Effects
{
   /// <summary>
   /// Identify a single GPU stage in the pipeline.
   /// </summary>
   public enum EffectShaderType:byte
   {
      /// <summary>
      /// Vertex shader stage.
      /// </summary>
      Vertex = 0,

      /// <summary>
      /// Hull shader stage.
      /// </summary>
      Hull = 1,

      /// <summary>
      /// Domain shader stage.
      /// </summary>
      Domain = 2,

      /// <summary>
      /// Geometry shader stage.
      /// </summary>
      Geometry = 3,

      /// <summary>
      /// Pixel shader stage.
      /// </summary>
      Fragment = 4,

      /// <summary>
      /// Compute shader stage.
      /// </summary>
      Compute = 5,
   }
}
