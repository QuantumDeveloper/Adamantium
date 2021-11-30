using Adamantium.Mathematics;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives
{
   public class DragStartedEventArgs:RoutedEventArgs
   {
      public Vector2D Offset { get; }

      public DragStartedEventArgs(Vector2D offset)
      {
         Offset = offset;
      }
   }
}
