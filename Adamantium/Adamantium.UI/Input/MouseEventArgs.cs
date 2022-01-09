using Adamantium.Mathematics;

namespace Adamantium.UI.Input;

public class MouseEventArgs:InputEventArgs
{
   public MouseDevice MouseDevice { get; }
      

   public Vector2 GetPosition(IInputElement relativeTo)
   {
      return MouseDevice.GetPosition(relativeTo);
   }

   public MouseEventArgs(MouseDevice device, InputModifiers modifiers, uint timestamp):base(modifiers, timestamp)
   {
      MouseDevice = device;
   }
}