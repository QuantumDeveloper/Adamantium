using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Input;

public class KeyboardFocusChangedEventArgs:RoutedEventArgs
{
   public IInputComponent OldFocus { get; private set; }
   public IInputComponent NewFocus { get; private set; }

   public KeyboardFocusChangedEventArgs(IInputComponent oldFocus, IInputComponent newFocus)
   {
      OldFocus = oldFocus;
      NewFocus = newFocus;
   }
}