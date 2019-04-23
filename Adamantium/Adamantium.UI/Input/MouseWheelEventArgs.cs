namespace Adamantium.UI.Input
{
   public class MouseWheelEventArgs:MouseEventArgs
   {
      public MouseWheelEventArgs(MouseDevice device, InputModifiers modifiers, int delta, uint timestamp):base(device, modifiers, timestamp)
      {
         Delta = delta;
      }

      public int Delta { get; private set; }
   }
}
