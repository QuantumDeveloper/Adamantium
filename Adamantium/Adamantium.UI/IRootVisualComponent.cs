namespace Adamantium.UI;

public interface IRootVisualComponent : IUIComponent
{
   /// <summary>
   /// Converts a point from screen to client coordinates.
   /// </summary>
   /// <param name="point">The point in screen coordinates.</param>
   /// <returns>The point in client coordinates.</returns>
   Vector2 PointToClient(Vector2 point);

   /// <summary>
   /// Converts a point from client to screen coordinates.
   /// </summary>
   /// <param name="point">The point in client coordinates.</param>
   /// <returns>The point in screen coordinates.</returns>
   Vector2 PointToScreen(Vector2 point);
}