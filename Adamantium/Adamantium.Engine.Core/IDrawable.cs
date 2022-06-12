using Adamantium.Core;

namespace Adamantium.Engine.Core
{
    /// <summary>
    /// An interface for a drawable game component that is called by the <see cref="SystemManager.Draw"/> class.
    /// </summary>
    public interface IDrawable
   {
      /// <summary>
      /// Starts the drawing of a frame. This method is followed by calls to Draw and EndDraw.
      /// </summary>
      /// <returns><c>true</c> if Draw should occur, <c>false</c> otherwise</returns>
      bool BeginDraw();

      /// <summary>
      /// Draws this instance.
      /// </summary>
      /// <param name="gameTime">The current timing.</param>
      void Draw(AppTime gameTime);

      /// <summary>
      /// Ends the drawing of a frame. This method is preceded by calls to BeginScene and Draw.
      /// </summary>
      void EndDraw();

      /// <summary>
      /// Gets a value indicating whether the <see cref="Draw"/> method should be called by <see cref="SystemManager.Draw"/>.
      /// </summary>
      /// <value><c>true</c> if this drawable component is visible; otherwise, <c>false</c>.</value>
      bool IsVisible { get; set; }

      /// <summary>
      /// Gets the draw order relative to other objects. <see cref="IDrawable"/> objects with a lower value are drawn first.
      /// </summary>
      /// <value>The draw order.</value>
      /// <remarks>This property is valid on if <see cref="ExecutionType"/> is <see cref="ExecutionType.Sync"/>. Otherwise priority will bw ignored</remarks>
      int DrawPriority { get; set; }

      /// <summary>
      /// Gets or sets the way how this system will be processed in Draw phase
      /// </summary>
      ExecutionType DrawExecutionType { get; set; }
   }
}
