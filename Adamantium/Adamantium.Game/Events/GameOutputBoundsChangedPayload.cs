using Adamantium.Mathematics;

namespace Adamantium.Game.Events
{
    public class GameOutputBoundsChangedPayload
    {
        public Rectangle Bounds { get; }

        public GameOutput Output { get; }

        public GameOutputBoundsChangedPayload(GameOutput output, Rectangle bounds)
        {
            Output = output;
            Bounds = bounds;
        }
    }
}