using Adamantium.Mathematics;

namespace Adamantium.Game.Core.Payloads
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