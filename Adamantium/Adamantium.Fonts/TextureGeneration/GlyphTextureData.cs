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

        public RectangleF UVRect;
        
        [Key(1)]
        public uint GlyphIndex { get; }

        [IgnoreMember]
        public byte[] Pixels { get; set; }

        public bool IsEmpty => Pixels == null || Pixels.Length == 0;

        public void CalculateUV(Size atlasSize)
        {
            RectangleF uvRect;
            uvRect.Left = (float)(BoundingRect.Left / atlasSize.Width);
            uvRect.Top = (float)(BoundingRect.Top / atlasSize.Height);
            uvRect.Right = (float)(BoundingRect.Right / atlasSize.Width);
            uvRect.Bottom = (float)(BoundingRect.Bottom / atlasSize.Height);
            UVRect = uvRect;
        }
    }
}
