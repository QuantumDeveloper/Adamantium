using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CMAP
{
    public class CharacterMapFormat6 : CharacterMap
    {
        public override UInt16 Format => 6;

        public UInt16 Length { get; set; }

        public UInt16 Language { get; set; }
        
        public UInt16 FirstCode { get; set; }
        
        public UInt16 EntryCount { get; set; }
        
        // array size is EntryCount
        public UInt16[] GlyphIdArray { get; set; }

        public override uint GetGlyphIndex(uint character)
        {
            var i = character - FirstCode;
            return (uint) (i >= 0 && i < GlyphIdArray.Length ? GlyphIdArray[i] : 0);
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            for (uint i = 0; i < GlyphIdArray.Length; ++i)
            {
                unicodes.Add(FirstCode + i);
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            for (uint i = 0; i < GlyphIdArray.Length; ++i)
            {
                unicodeToGlyph[FirstCode + i] = i;
            }
        }
    }
}