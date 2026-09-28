using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CMAP
{
    /// <summary>
    /// mixed 16-bit and 32-bit coverage
    /// </summary>
    public class CharacterMapFormat8 : CharacterMap
    {
        public CharacterMapFormat8()
        {
            Is32 = new byte[8192];
        }

        public override UInt16 Format => 8;
        
        public UInt16 Reserved { get; set; }

        public UInt16 Length { get; set; }

        public UInt32 Language { get; set; }
        
        public byte[] Is32 { get; }
        
        public UInt32 NumGroups { get; set; }
        
        // array size is NumGroups
        public SequentialMapGroup[] Groups { get; set; }
        
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