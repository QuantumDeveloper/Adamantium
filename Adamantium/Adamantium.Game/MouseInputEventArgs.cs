using System;
using Adamantium.Game.GameInput;

namespace Adamantium.Game
{
    public class MouseInputEventArgs : EventArgs
    {
        public MouseInputEventArgs(MouseInput mouseInput)
        {
            MouseInput = mouseInput;
        }

        public MouseInput MouseInput { get; }

    }
}
