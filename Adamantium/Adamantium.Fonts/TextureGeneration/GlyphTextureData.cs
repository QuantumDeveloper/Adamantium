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

        public GlyphTextureData(uint width, uint height, uint glyphIndex, uint margin, char character)
        {
            Character = character;
            BoundingRect.Width = (int)width;
            BoundingRect.Height = (int)height;
            Pixels = new byte[(BoundingRect.Width + (margin * 2)) * (BoundingRect.Height + (margin * 2)) * 4];
            GlyphIndex = glyphIndex;
            Margin = margin;
            FullGlyphSize = new Size(width + (Margin * 2), height + (Margin * 2));
        }
        
        public char Character { get; set; }

        [Key(0)]
        public Rectangle BoundingRect;

        public RectangleF UVRect;
        
        [Key(1)]
        public uint GlyphIndex { get; }

        [IgnoreMember]
        public byte[] Pixels { get; set; }

        public bool IsEmpty => Pixels == null || Pixels.Length == 0;
        
        public uint Margin { get; }

        public Size FullGlyphSize { get; }

        public void CalculateUV(Size atlasSize)
        {
            RectangleF uvRect;
            uvRect.Left = (float)((BoundingRect.Left + Margin) / atlasSize.Width);
            uvRect.Top = (float)((BoundingRect.Top + Margin) / atlasSize.Height);
            uvRect.Right = (float)((BoundingRect.Right + Margin) / atlasSize.Width);
            uvRect.Bottom = (float)((BoundingRect.Bottom + Margin) / atlasSize.Height);
            UVRect = uvRect;
            
            // RectangleF uvRect;
            // uvRect.Left = (BoundingRect.Left + Margin);
            // uvRect.Top = (BoundingRect.Top + Margin);
            // uvRect.Right = (BoundingRect.Right + Margin);
            // uvRect.Bottom = (BoundingRect.Bottom + Margin);
            // UVRect = uvRect;
        }

        public override string ToString()
        {
            return $"Index: {GlyphIndex}, Related Char: {Character}, Rect: {BoundingRect}";
        }
    }
}
