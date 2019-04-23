using System;
using Adamantium.Engine.GameInput;

namespace Adamantium.Engine
{
    public class KeyboardInputEventArgs : EventArgs
    {
        public KeyboardInputEventArgs(KeyboardInput keyboardInput)
        {
            KeyboardInput = keyboardInput;
        }

        public KeyboardInput KeyboardInput { get; }
    }
}
