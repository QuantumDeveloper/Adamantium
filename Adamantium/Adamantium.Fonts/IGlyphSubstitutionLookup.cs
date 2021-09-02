namespace Adamantium.Fonts
{
    public interface IGlyphSubstitutionLookup
    {
        uint Count { get; }
        
        void Replace(uint glyphIndex, uint substitutionGlyphIndex);

        void Replace(uint glyphIndex, params uint[] substitutionArray);
        
        void Replace(uint glyphIndex, params ushort[] substitutionArray);

        void Replace(uint glyphIndex, int removeLength, ushort replaceIndex);
    }
}