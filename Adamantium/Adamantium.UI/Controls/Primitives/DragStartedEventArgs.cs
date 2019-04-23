using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.Primitives
{
   public class DragStartedEventArgs:RoutedEventArgs
   {
      public Point Offset { get; }

      public DragStartedEventArgs(Point offset)
      {
         Offset = offset;
      }
   }
}
