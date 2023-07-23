using Adamantium.Mathematics;

namespace Adamantium.Imaging.PaletteQuantizer
{
    public class QuantizerResult
    {
        public FrameData Image { get; set; }

        public Color[] ColorTable { get; set; }

        public byte[] CompressedPixels { get; set; }

        public int[] IndexTable { get; set; }

        public QuantizerResult()
        {
        }

        public QuantizerResult(FrameData image, Color[] colorTable, int[] indexTable)
        {
            Image = image;
            ColorTable = colorTable;
            IndexTable = indexTable;
        }
    }
}
