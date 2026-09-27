using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables
{
    internal class LigatureCaretList
    {
        public CoverageTable Coverage { get; set; }
        
        public LigatureGlyph[] LigatureGlyphs { get; set; }
    }
}