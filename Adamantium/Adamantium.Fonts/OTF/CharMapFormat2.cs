using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public class CharMapFormat2 : CharacterMap
    {
        public CharMapFormat2()
        {
            SubheaderKeys = new UInt16[256];
        }

        public override UInt16 Format => 2;

        public UInt16 Length { get; set; }

        public UInt16 Language { get; set; }

        // 256 values
        public UInt16[] SubheaderKeys { get; }

        public SubHeader[] SubHeaders { get; set; }

        public UInt16[] GlyphIdArray { get; set; }
        
        public override uint GetGlyphIndex(uint character)
        {
            throw new NotImplementedException();
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            throw new NotImplementedException();
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            throw new NotImplementedException();
        }
    }
}