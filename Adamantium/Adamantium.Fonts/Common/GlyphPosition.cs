using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    public struct GlyphPosition
    {
        public Vector2F Offset;

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