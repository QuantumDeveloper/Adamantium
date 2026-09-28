using System;
using Adamantium.Fonts;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

public class GlyphWordData
{
    public GlyphWordData(Glyph glyph, Char symbol, RectangleF rect, int positionInString, int lineIndex)
    {
        Glyph = glyph;
        Rect = rect;
        Symbol = symbol;
        PositionInString = positionInString;
        LineIndex = lineIndex;
    }
    
    public Char Symbol { get; }

    public Glyph Glyph { get; }

    public RectangleF Rect;

    public int PositionInString { get; set; }
    
    public int LineIndex { get; set; }

    public override string ToString()
    {
        return $"{Symbol} [{Rect}]";
    }
}