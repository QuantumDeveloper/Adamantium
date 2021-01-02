using System;
using Adamantium.Mathematics;

namespace Adamantium.Game
{
    public class GameWindowBoundsChangedEventArgs : EventArgs
    {
        public Rectangle Bounds { get; }

        public GameOutput Window { get; }

        public GameWindowBoundsChangedEventArgs(GameOutput window, Rectangle bounds)
        {
            Window = window;
            Bounds = bounds;
        }
    }
}
