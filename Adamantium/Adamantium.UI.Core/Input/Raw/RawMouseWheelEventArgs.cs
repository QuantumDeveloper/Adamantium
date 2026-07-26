using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input.Raw;

public class RawMouseWheelEventArgs : RawMouseEventArgs
{
   public RawMouseWheelEventArgs(
      Int32 wheelDelta, 
      RawMouseEventType eventType, 
      IInputComponent rootComponent,
      Vector2 position, 
      InputModifiers modifiers, 
      MouseDevice device,
      UInt32 timeStep,
      bool isHorizontal = false)
      : base(eventType, rootComponent, position, modifiers, device, timeStep)
   {
      WheelDelta = wheelDelta;
      IsHorizontal = isHorizontal;
   }

   public Int32 WheelDelta { get; private set; }

   /// <summary>True for a horizontal (tilt) wheel - WM_MOUSEHWHEEL - so the consumer scrolls the X axis.</summary>
   public bool IsHorizontal { get; private set; }
}