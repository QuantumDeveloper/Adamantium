using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

public class DragStartedEventArgs:RoutedEventArgs
{
   public Vector2 Offset { get; }

   public DragStartedEventArgs(Vector2 offset)
   {
      Offset = offset;
   }
}