namespace Adamantium.UI.Input;

public abstract class InputDevice
{
   public abstract IInputElement TargetElement { get; protected set; }
}