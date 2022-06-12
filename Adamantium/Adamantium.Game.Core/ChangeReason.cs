using Adamantium.Engine.Graphics;

namespace Adamantium.Game
{
   /// <summary>
   /// Describes reason of <see cref="GraphicsPresenter"/> parameters change reason
   /// </summary>
   public enum ChangeReason
   {
      /// <summary>
      /// Full <see cref="GraphicsPresenter"/> was updated
      /// </summary>
      FullUpdate,

      /// <summary>
      /// Only DepthBuffer was updated
      /// </summary>
      DepthBufferOnly,

      /// <summary>
      /// <see cref="GraphicsPresenter"/> was resized
      /// </summary>
      Resize,
   }
}
