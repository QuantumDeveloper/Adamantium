using Adamantium.Mathematics;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives
{
   public class DragEventArgs:RoutedEventArgs
   {
      public Point Change { get; }

      public DragEventArgs(Point changedPoint)
      {
         Change = changedPoint;
      }
   }
}
