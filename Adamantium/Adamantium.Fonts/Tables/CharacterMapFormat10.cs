using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables
{
    /// <summary>
    /// Trimmed array
    /// This format is not widely used and is not supported by Microsoft.
    /// It would be most suitable for fonts that support only a contiguous
    /// range of Unicode supplementary-plane characters, but such fonts are rare. 
    /// </summary>
    public class CharacterMapFormat10 : CharacterMap
    {
        public override UInt16 Format => 10;
        
        public UInt16 Reserved { get; set; }

        public UInt32 Length { get; set; }

        public UInt32 Language { get; set; }
        
        public UInt32 StartCharCode { get; set; }
        
        public UInt32 NumChars { get; set; }
        
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