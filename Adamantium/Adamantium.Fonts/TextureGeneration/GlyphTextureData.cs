using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.TextureGeneration
{
    [MessagePackObject]
    public class GlyphTextureData
    {
        [SerializationConstructor]
        public GlyphTextureData()
        {

        }

        public GlyphTextureData(uint width, uint height, uint glyphIndex)
        {
            BoundingRect.Width = (int)width;
            BoundingRect.Height = (int)height;
            Pixels = new byte[width * height * 4];
            GlyphIndex = glyphIndex;
        }

        [Key(0)]
        public Rectangle BoundingRect;
        
        [Key(1)] 
        public RectangleF UV;
        
        [Key(2)]
        public uint GlyphIndex { get; }

        [IgnoreMember]
        public byte[] Pixels { get; set; }
    }
}
