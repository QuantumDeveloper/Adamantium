using Adamantium.UI.Input;

namespace Adamantium.UI;

public interface ITrigger
{
   SetterCollection Setters { get; set; }
   void Apply(IInputComponent control);
}