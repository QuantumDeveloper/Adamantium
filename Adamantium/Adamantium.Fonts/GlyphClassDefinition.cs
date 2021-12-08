namespace Adamantium.Fonts
{
    public enum GlyphClassDefinition
    {
        Zero = 0,
        
        /// <summary>
        /// Base glyph (single character, spacing glyph)
        /// </summary>
        Base = 1,
        
        /// <summary>
        /// Ligature glyph (multiple character, spacing glyph)
        /// </summary>
        Ligature = 2,
        
        /// <summary>
        /// Mark glyph (non-spacing combining glyph)
        /// </summary>
        Mark = 3,
        
        /// <summary>
        /// Component glyph (part of single character, spacing glyph)
        /// </summary>
        Component = 4
    }
}