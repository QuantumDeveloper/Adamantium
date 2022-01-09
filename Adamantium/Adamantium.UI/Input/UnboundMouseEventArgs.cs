using Adamantium.Mathematics;

namespace Adamantium.UI.Input;

public class UnboundMouseEventArgs:MouseEventArgs
{
   public Vector2 Delta { get; }

   public UnboundMouseEventArgs(MouseDevice device, Vector2 mouseDelta, InputModifiers modifiers, uint timestamp) : base(device, modifiers, timestamp)
   {
      Delta = mouseDelta;
   }
}