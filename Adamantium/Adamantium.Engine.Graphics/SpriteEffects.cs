namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Possible sprite effects
   /// </summary>
   public enum SpriteEffects
   {
      /// <summary>
      /// No effect
      /// </summary>
      None = 0,
      
      /// <summary>
      /// Flip sprite horizontally
      /// </summary>
      FlipHorizontally = 1,
      
      /// <summary>
      /// Flip sprite vertically
      /// </summary>
      FlipVertically = 2,

      /// <summary>
      /// Flip sprite horizontally and vertically
      /// </summary>
      FlipBoth = FlipHorizontally|FlipVertically
   }
}
