using Adamantium.Mathematics;

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
