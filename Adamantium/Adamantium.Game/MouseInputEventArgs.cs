using System;
using Adamantium.Engine.GameInput;

namespace Adamantium.Engine
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
