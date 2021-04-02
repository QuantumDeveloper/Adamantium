using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public abstract class CharacterMap
    {
        public abstract UInt16 Format { get; }
        
        public UInt16 PlatformId { get; set; }
        
        public UInt16 EncodingId { get; set; }

        public abstract uint GetGlyphIndex(uint character);

        public abstract void CollectUnicodeChars(List<uint> unicodes);

        public abstract void GetUnicodeToGlyphMappings(Dictionary<UInt32, UInt32> unicodeToGlyph);

    }
}