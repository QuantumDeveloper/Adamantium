using Adamantium.Mathematics;

namespace Adamantium.Multiverse.Payloads
{
    public class UniverseOutputSizeChangedPayload
    {
        public Size Size { get; }

        public UniverseOutput Output { get; }

        public UniverseOutputSizeChangedPayload(UniverseOutput output, Size size)
        {
            Output = output;
            Size = size;
        }
    }
}