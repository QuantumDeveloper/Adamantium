namespace Adamantium.UI.Core.Input;

public abstract class InputDevice
{
   public abstract IInputComponent TargetComponent { get; protected set; }
}