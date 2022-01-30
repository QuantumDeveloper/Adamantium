namespace Adamantium.UI.Input;

public class KeyboardGotFocusEventArgs: KeyboardFocusChangedEventArgs
{
   public InputModifiers Modifiers { get; private set; }
   public NavigationMethod NavigationMethod { get; private set; }

   public KeyboardGotFocusEventArgs(IInputComponent oldFocus, IInputComponent newFocus, NavigationMethod navigationMethod, InputModifiers modifiers) : base(oldFocus, newFocus)
   {
      Modifiers = modifiers;
      NavigationMethod = navigationMethod;
   }
}