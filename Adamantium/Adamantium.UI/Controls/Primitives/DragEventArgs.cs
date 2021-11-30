using Adamantium.Mathematics;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives
{
   public class DragEventArgs:RoutedEventArgs
   {
      public Vector2D Change { get; }

      public DragEventArgs(Vector2D changedPoint)
      {
         Change = changedPoint;
      }
   }
}
