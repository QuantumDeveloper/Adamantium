using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CMAP
{
    public class CharacterMapFormat0 : CharacterMap
    {
        public override UInt16 Format => 0;
        
        public UInt16 Length { get; set; }

        public UInt16 Language { get; set; }

        // 256 items
        public byte[] GlyphIdArray { get; set; }
        
        public override uint GetGlyphIndex(uint character)
        {
            if (character > 255) return 0;
            
            return GlyphIdArray[character];
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            for (uint i = 0; i < GlyphIdArray.Length; ++i)
            {
                unicodes.Add(i);
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            for (uint i = 0; i < GlyphIdArray.Length; ++i)
            {
                unicodeToGlyph[i] = i;
            }
        }
    }
}