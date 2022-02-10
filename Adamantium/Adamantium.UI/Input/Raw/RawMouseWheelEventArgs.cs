using System;

namespace Adamantium.UI.Input.Raw;

public class RawMouseWheelEventArgs : RawMouseEventArgs
{
   public RawMouseWheelEventArgs(
      Int32 wheelDelta, 
      RawMouseEventType eventType, 
      IInputComponent rootComponent,
      Vector2 position, 
      InputModifiers modifiers, 
      MouseDevice device, 
      UInt32 timeStep)
      : base(eventType, rootComponent, position, modifiers, device, timeStep)
   {
      WheelDelta = wheelDelta;
   }

   public Int32 WheelDelta { get; private set; }
}