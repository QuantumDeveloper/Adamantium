using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public interface IGlyphPositioning
    {
        uint Count { get; }

        uint GetGlyphIndex(uint index);
        
        GlyphClassDefinition GetGlyphClassDefinition(uint index);
        
        void AppendGlyphOffset(FeatureInfo featureInfo, uint glyphIndex, Vector2F offset);

        void AppendGlyphAdvance(FeatureInfo featureInfo, uint glyphIndex, Vector2F advance);

        Vector2F GetOffset(uint glyphIndex);

        Vector2F GetAdvance(uint glyphIndex);   
    }
}