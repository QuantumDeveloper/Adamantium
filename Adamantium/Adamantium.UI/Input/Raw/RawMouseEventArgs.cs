using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Input.Raw
{
    public class RawMouseEventArgs : RawInputEventArgs
    {
        public RawMouseEventArgs(RawMouseEventType eventType, IInputElement rootElement, Vector2 position, InputModifiers modifiers, MouseDevice device, UInt32 timeStep)
           : base(modifiers, timeStep)
        {
            MouseDevice = device;
            EventType = eventType;
            RootElement = rootElement;
            Position = position;
        }

        public MouseDevice MouseDevice { get; private set; }
        public RawMouseEventType EventType { get; private set; }
        public IInputElement RootElement { get; private set; }
        public Vector2 Position { get; private set; }
    }
}
