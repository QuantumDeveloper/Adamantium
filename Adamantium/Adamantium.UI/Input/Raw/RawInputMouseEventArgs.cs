using Adamantium.Mathematics;

namespace Adamantium.UI.Input.Raw
{
   internal class RawInputMouseEventArgs : RawMouseEventArgs
   {
      public Point Delta { get; }

      public RawInputMouseEventArgs(Point delta, RawMouseEventType eventType, IInputElement rootElement, Point position,
         InputModifiers modifiers, MouseDevice device, uint timeStep)
         : base(eventType, rootElement, position, modifiers, device, timeStep)
      {
         Delta = delta;
      }
   }
}
