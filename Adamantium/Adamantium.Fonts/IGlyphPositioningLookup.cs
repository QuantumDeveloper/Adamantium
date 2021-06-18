namespace Adamantium.Fonts
{
    public interface IGlyphPositioningLookup
    {
        GlyphClassDefinition GetGlyphClassDefinition(uint index);
        
        GlyphPositionData GetPosition(uint glyphIndex);
    }
}