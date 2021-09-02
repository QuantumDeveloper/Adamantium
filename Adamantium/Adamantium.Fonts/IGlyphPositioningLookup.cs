using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public interface IGlyphPositioningLookup
    {
        uint Count { get; }
        
        GlyphClassDefinition GetGlyphClassDefinition(uint index);
        
        void AppendGlyphOffset(FontLanguage language, FeatureInfo featureInfo, uint glyphIndex, Vector2F offset);

        void AppendGlyphAdvance(FontLanguage language, FeatureInfo feature, uint glyphIndex, Vector2F advance);

        Vector2F GetOffset(uint glyphIndex);

        Vector2F GetAdvance(uint glyphIndex);   
    }
}