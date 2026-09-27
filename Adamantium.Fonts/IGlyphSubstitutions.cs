using Adamantium.Fonts.Common;

namespace Adamantium.Fonts
{
    public interface IGlyphSubstitutions
    {
        uint Count { get; }
        
        uint GetGlyphIndex(uint index);
        
        void Replace(FeatureInfo featureInfo, uint glyphIndex, uint substitutionGlyphIndex);

        void Replace(FeatureInfo featureInfo, uint glyphIndex, uint[] substitutionArray);
        
        void Replace(FeatureInfo featureInfo, uint glyphIndex, ushort[] substitutionArray);

        void Replace(FeatureInfo featureInfo, uint glyphIndex, int removeLength, ushort newGlyphIndex);
    }
}