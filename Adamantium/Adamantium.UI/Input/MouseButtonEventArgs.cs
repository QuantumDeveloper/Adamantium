namespace Adamantium.UI.Input;

public class MouseButtonEventArgs:MouseEventArgs
{
   public MouseButtonState ButtonState { get; private set; }
   public MouseButtons ChangedButton { get; private set; }

      
   public int ClickCount { get; internal set; }

   public MouseButtonEventArgs(MouseDevice device, MouseButtons changedButton, MouseButtonState buttonState,
      InputModifiers modifiers, uint timestamp) : base(device, modifiers, timestamp)
   {
      ButtonState = buttonState;
      ChangedButton = changedButton;
   }

}