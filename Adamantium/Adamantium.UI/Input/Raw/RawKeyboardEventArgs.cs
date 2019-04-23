using System;

namespace Adamantium.UI.Input.Raw
{
   internal class RawKeyboardEventArgs:RawInputEventArgs
   {
      public Key ChangedKey { get; }
      public RawKeyboardEventType EventType { get; }
      public IntPtr LParam { get; }

      public RawKeyboardEventArgs(Key changedKey, RawKeyboardEventType eventType, IntPtr lParam, InputModifiers modifiers, uint timeStep) : base(modifiers, timeStep)
      {
         ChangedKey = changedKey;
         EventType = eventType;
         LParam = lParam;
      }
   }
}
