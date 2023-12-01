using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.TextureGeneration
{
    [MessagePackObject]
    public class GlyphTextureData
    {
        public GlyphTextureData(uint width, uint height, uint glyphIndex)
        {
            Width = width;
            Height = height;
            Pixels = new byte[Width * Height * 4];
            GlyphIndex = glyphIndex;
        }

        [Key(0)]
        public uint X { get; set; }
        [Key(1)]
        public uint Y { get; set; }
        [Key(4)]
        public uint Width { get; }
        [Key(5)]
        public uint Height { get; }
        [Key(2)]
        public RectangleF UV { get; set; }
        [Key(6)]
        public uint GlyphIndex { get; }
        [IgnoreMember]
        public byte[] Pixels { get; set; }
    }
}
