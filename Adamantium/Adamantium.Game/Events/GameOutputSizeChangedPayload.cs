using Adamantium.UI;

namespace Adamantium.Game.Events
{
    public class GameOutputSizeChangedPayload
    {
        public Size Size { get; }

        public GameOutput Output { get; }

        public GameOutputSizeChangedPayload(GameOutput output, Size size)
        {
            Output = output;
            Size = size;
        }
    }
}