using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Parsers;

internal class TTFGlyphOutlineSource : IGlyphOutlineSource
{
    private readonly SfntParser parser;
    private readonly byte[] fontData;
    private readonly long glyfTableOffset;
    private readonly uint[] glyphOffsets;
    private readonly Glyph[] glyphs;

    public TTFGlyphOutlineSource(SfntParser parser, byte[] fontData, long glyfTableOffset, uint[] glyphOffsets, Glyph[] glyphs)
    {
        this.parser = parser;
        this.fontData = fontData;
        this.glyfTableOffset = glyfTableOffset;
        this.glyphOffsets = glyphOffsets;
        this.glyphs = glyphs;
    }

    public void LoadOutlines(Glyph glyph)
    {
        using var reader = new FontStreamReader(fontData);
        reader.Position = glyfTableOffset + glyphOffsets[glyph.Index];
        parser.ReadGlyphOutlines(reader, glyph, glyphs);
    }
}
