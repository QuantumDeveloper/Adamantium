using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

public class DragEventArgs:RoutedEventArgs
{
   public Vector2 Change { get; }

   public DragEventArgs(Vector2 changedPoint)
   {
      Change = changedPoint;
   }
}