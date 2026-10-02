namespace Adamantium.Fonts.Parsers;

internal class CompositeGlyphOutlineSource : IGlyphOutlineSource
{
    private readonly Glyph[] glyphs;

    public CompositeGlyphOutlineSource(Glyph[] glyphs)
    {
        this.glyphs = glyphs;
    }

    public void LoadOutlines(Glyph glyph)
    {
        glyph.AddComponentOutlines(glyphs);
    }
}
