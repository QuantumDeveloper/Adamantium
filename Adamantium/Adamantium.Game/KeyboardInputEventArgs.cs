using System;
using Adamantium.Game.GameInput;

namespace Adamantium.Game
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
