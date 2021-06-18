namespace Adamantium.Fonts
{
    public interface IGlyphSubstitutionLookup
    {
        void Replace(uint glyphIndex, uint substitutionGlyphIndex);
        
        
    }
}