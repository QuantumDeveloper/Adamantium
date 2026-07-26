namespace Adamantium.UI.Core.Input;

public class MouseWheelEventArgs:MouseEventArgs
{
   public MouseWheelEventArgs(MouseDevice device, InputModifiers modifiers, int delta, uint timestamp, bool isHorizontal = false):base(device, modifiers, timestamp)
   {
      Delta = delta;
      IsHorizontal = isHorizontal;
   }

   public int Delta { get; private set; }

   /// <summary>True for a horizontal (tilt) wheel; the handler scrolls the X axis instead of Y.</summary>
   public bool IsHorizontal { get; private set; }
}