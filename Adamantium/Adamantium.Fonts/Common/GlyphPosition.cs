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
    }
}