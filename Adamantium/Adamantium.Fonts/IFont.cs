using System;
using System.Collections.Generic;

namespace Adamantium.Fonts
{
    public interface IFont
    {
        string Name { get; }
        
        UInt32 Unicode { get; }
        
        IReadOnlyCollection<UInt32> Unicodes { get; }
        
        IReadOnlyCollection<Glyph> Glyphs { get; }

        Glyph GetGlyphByName(string name);

        Glyph GetGlyphByUnicode(UInt32 unicode);

        Glyph GetGlyphByIndex(UInt32 index);
    }
}