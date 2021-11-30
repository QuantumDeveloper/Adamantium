using Adamantium.Mathematics;

namespace Adamantium.UI.Input
{
   public class UnboundMouseEventArgs:MouseEventArgs
   {
      public Vector2D Delta { get; }

      public UnboundMouseEventArgs(MouseDevice device, Vector2D mouseDelta, InputModifiers modifiers, uint timestamp) : base(device, modifiers, timestamp)
      {
         Delta = mouseDelta;
      }
   }
}
