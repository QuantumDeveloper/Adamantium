using System;
using Adamantium.Mathematics;

namespace Adamantium.Engine
{
    public class GameWindowBoundsChangedEventArgs : EventArgs
    {
        public Rectangle Bounds { get; }

        public GameWindow Window { get; }

        public GameWindowBoundsChangedEventArgs(GameWindow window, Rectangle bounds)
        {
            Window = window;
            Bounds = bounds;
        }
    }
}
