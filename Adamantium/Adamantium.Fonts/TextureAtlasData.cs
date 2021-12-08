using Adamantium.Mathematics;
using Adamantium.UI;

namespace Adamantium.Fonts
{
    public class TextureAtlasData
    {
        public Color[] AtlasColors { get; }
        public Size AtlasSize { get; }

        internal TextureAtlasData(Color[] colors, Size size)
        {
            AtlasColors = colors;
            AtlasSize = size;
        }
    }
}