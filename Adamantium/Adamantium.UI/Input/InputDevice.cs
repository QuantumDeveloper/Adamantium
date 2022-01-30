namespace Adamantium.UI.Input;

public abstract class InputDevice
{
   public abstract IInputComponent TargetComponent { get; protected set; }
}