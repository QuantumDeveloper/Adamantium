using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF;

internal class CFFGlyphOutlineSource : IGlyphOutlineSource
{
    private readonly ICFFParser parser;
    private readonly CFFFont font;
    private readonly FontDict[] fontDicts;

    public CFFGlyphOutlineSource(ICFFParser parser, CFFFont font, FontDict[] fontDicts)
    {
        this.parser = parser;
        this.font = font;
        this.fontDicts = fontDicts;
    }

    public void LoadOutlines(Glyph glyph)
    {
        var index = (int)glyph.Index;
        var data = font.CharStringsIndex.DataByOffset[index];
        var stack = new Stack<byte>(data.Length);
        for (var j = data.Length - 1; j >= 0; --j)
        {
            stack.Push(data[j]);
        }

        var commands = new CommandParser(parser).Parse(font, stack, fontDicts[index], index);
        glyph.FillOutlines(commands).RecalculateBounds();
    }
}
