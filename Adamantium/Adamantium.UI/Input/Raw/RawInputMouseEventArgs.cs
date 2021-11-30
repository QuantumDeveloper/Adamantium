using Adamantium.Mathematics;

namespace Adamantium.UI.Input.Raw
{
   public class RawInputMouseEventArgs : RawMouseEventArgs
   {
      public Vector2D Delta { get; }

      public RawInputMouseEventArgs(Vector2D delta, RawMouseEventType eventType, IInputElement rootElement, Vector2D position,
         InputModifiers modifiers, MouseDevice device, uint timeStep)
         : base(eventType, rootElement, position, modifiers, device, timeStep)
      {
         Delta = delta;
      }
   }
}
