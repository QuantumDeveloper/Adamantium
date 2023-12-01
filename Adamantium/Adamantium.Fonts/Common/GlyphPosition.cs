using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    public struct GlyphPosition
    {
        [Key(0)]
        public Vector2F Offset;

        [Key(1)]
        public Vector2F Advance;

        public static GlyphPosition operator +(GlyphPosition left, GlyphPosition right)
        {
            left.Offset += right.Offset;
            left.Advance += right.Advance;

            return left;
        }

        public static GlyphPosition operator -(GlyphPosition left, GlyphPosition right)
        {
            left.Offset -= right.Offset;
            left.Advance -= right.Advance;

            return left;
        }

        public static GlyphPosition FromOffset(Vector2F offset)
        {
            return new GlyphPosition() { Offset = offset };
        }
        
        public static GlyphPosition FromAdvance(Vector2F advance)
        {
            return new GlyphPosition() { Advance = advance };
        }
    }
}