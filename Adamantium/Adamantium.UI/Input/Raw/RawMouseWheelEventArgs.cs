using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Input.Raw
{
   public class RawMouseWheelEventArgs : RawMouseEventArgs
   {
      public RawMouseWheelEventArgs(
         Int32 wheelDelta, 
         RawMouseEventType eventType, 
         IInputElement rootElement,
         Vector2 position, 
         InputModifiers modifiers, 
         MouseDevice device, 
         UInt32 timeStep)
         : base(eventType, rootElement, position, modifiers, device, timeStep)
      {
         WheelDelta = wheelDelta;
      }

      public Int32 WheelDelta { get; private set; }
   }
}
