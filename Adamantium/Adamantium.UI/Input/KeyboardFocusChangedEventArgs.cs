namespace Adamantium.UI.Input
{
   public class KeyboardFocusChangedEventArgs:RoutedEventArgs
   {
      public IInputElement OldFocus { get; private set; }
      public IInputElement NewFocus { get; private set; }

      public KeyboardFocusChangedEventArgs(IInputElement oldFocus, IInputElement newFocus)
      {
         OldFocus = oldFocus;
         NewFocus = newFocus;
      }
   }
}
