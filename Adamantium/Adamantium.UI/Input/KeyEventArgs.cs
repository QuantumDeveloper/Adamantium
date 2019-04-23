namespace Adamantium.UI.Input
{
   public class KeyEventArgs:InputEventArgs
   {
      public KeyEventArgs(KeyboardDevice device, Key key, InputModifiers modifiers, uint timestamp) : base(modifiers, timestamp)
      {
         Device = device;
         Key = key;
         IsDown = Keyboard.IsKeyDown(key);
         IsUp = !IsDown;
         IsToggled = Keyboard.IsKeyToggled(key);
         IsRepeated = device.IsRepeated(key);
      }

      public KeyboardDevice Device { get; }

      public Key Key { get; }

      public bool IsDown { get;}

      public bool IsUp { get;}

      public bool IsRepeated { get; }

      public bool IsToggled { get; }

   }
}
