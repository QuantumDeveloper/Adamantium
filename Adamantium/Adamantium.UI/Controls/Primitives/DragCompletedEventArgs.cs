using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.Primitives
{
   public class DragCompletedEventArgs:DragEventArgs
   {
      public bool IsCancelled { get; }
      public DragCompletedEventArgs(Point changedPoint, bool isCancelled) : base(changedPoint)
      {
         IsCancelled = isCancelled;
      }
   }
}
