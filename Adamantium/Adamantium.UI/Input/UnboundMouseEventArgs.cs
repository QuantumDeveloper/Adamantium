using Adamantium.Mathematics;

namespace Adamantium.UI.Input
{
   public class UnboundMouseEventArgs:MouseEventArgs
   {
      public Point Delta { get; }

      public UnboundMouseEventArgs(MouseDevice device, Point mouseDelta, InputModifiers modifiers, uint timestamp) : base(device, modifiers, timestamp)
      {
         Delta = mouseDelta;
      }
   }
}
