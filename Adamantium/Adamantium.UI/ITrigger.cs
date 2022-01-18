namespace Adamantium.UI;

public interface ITrigger
{
   SetterCollection Setters { get; set; }
   void Apply(IUIComponent control);
}