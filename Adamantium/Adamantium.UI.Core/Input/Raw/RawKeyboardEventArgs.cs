namespace Adamantium.UI.Core.Input.Raw;

public class RawKeyboardEventArgs : RawInputEventArgs
{
   public Key ChangedKey { get; }
   public RawKeyboardEventType EventType { get; }

   /// <summary>What the OS reported about the press itself (previous state, repeat count). The platform decodes this
   /// out of its own message format before handing the event over - this layer never sees a raw LPARAM.</summary>
   public KeyPressInfo Press { get; }

   public RawKeyboardEventArgs(Key changedKey, RawKeyboardEventType eventType, KeyPressInfo press,
      InputModifiers modifiers, uint timeStep) : base(modifiers, timeStep)
   {
      ChangedKey = changedKey;
      EventType = eventType;
      Press = press;
   }
}