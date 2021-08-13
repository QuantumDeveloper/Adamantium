using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public interface IGlyphPositioningLookup
    {
        public uint Count { get; }
        
        GlyphClassDefinition GetGlyphClassDefinition(uint index);
        
        void AppendGlyphOffset(uint glyphIndex, Vector2F offset);

        void AppendGlyphAdvance(uint glyphIndex, Vector2F advance);

        Vector2F GetOffset(uint glyphIndex);

        Vector2F GetAdvance(uint glyphIndex);   
    }
}