using Adamantium.Mathematics;

namespace Adamantium.UI.Input.Raw
{
   public class RawInputMouseEventArgs : RawMouseEventArgs
   {
      public Vector2 Delta { get; }

      public RawInputMouseEventArgs(Vector2 delta, RawMouseEventType eventType, IInputElement rootElement, Vector2 position,
         InputModifiers modifiers, MouseDevice device, uint timeStep)
         : base(eventType, rootElement, position, modifiers, device, timeStep)
      {
         Delta = delta;
      }
   }
}
