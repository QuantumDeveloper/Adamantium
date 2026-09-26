namespace Adamantium.UI.Core.Input;

public class KeyEventArgs:InputEventArgs
{
   public KeyEventArgs(KeyboardDevice device, Key key, InputModifiers modifiers, uint timestamp, uint physicalKey = 0)
      : base(modifiers, timestamp)
   {
      Device = device;
      Key = key;
      PhysicalKey = physicalKey;
      IsDown = Keyboard.IsKeyDown(key);
      IsUp = !IsDown;
      IsToggled = Keyboard.IsKeyToggled(key);
      IsRepeated = device.IsRepeated(key);
   }

   public KeyboardDevice Device { get; }

   public Key Key { get; }

   /// <summary>Where the key is on the keyboard, as <see cref="KeyPressInfo.PhysicalKey"/>; 0 when not known.</summary>
   public uint PhysicalKey { get; }

   public bool IsDown { get;}

   public bool IsUp { get;}

   public bool IsRepeated { get; }

   public bool IsToggled { get; }

}