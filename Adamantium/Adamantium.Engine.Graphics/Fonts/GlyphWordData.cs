using System;
using Adamantium.Fonts;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics.Fonts;

public class GlyphWordData
{
    public GlyphWordData(Glyph glyph, Char symbol, RectangleF rect, int positionInString)
    {
        Glyph = glyph;
        Rect = rect;
        Symbol = symbol;
        PositionInString = positionInString;
    }
    
    public Char Symbol { get; }

    public Glyph Glyph { get; }

    public RectangleF Rect;

    public int PositionInString { get; set; }
}