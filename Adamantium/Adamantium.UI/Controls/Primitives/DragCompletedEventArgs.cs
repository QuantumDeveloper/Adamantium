using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.Primitives
{
   public class DragCompletedEventArgs:DragEventArgs
   {
      public bool IsCancelled { get; }
      public DragCompletedEventArgs(Vector2D changedPoint, bool isCancelled) : base(changedPoint)
      {
         IsCancelled = isCancelled;
      }
   }
}
