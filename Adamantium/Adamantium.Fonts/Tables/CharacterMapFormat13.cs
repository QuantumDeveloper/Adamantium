using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Tables
{
    public class CharacterMapFormat13 : CharacterMap
    {
        public override UInt16 Format => 13;
        
        public UInt16 Reserved { get; set; }

        public UInt32 Length { get; set; }

        public UInt32 Language { get; set; }
        
        public UInt32 NumGroups { get; set; }
        
        public ConstantMapGroup[] Groups { get; set; }
        
        public override uint GetGlyphIndex(uint character)
        {
            var group = Groups.FirstOrDefault(x => x.StartCharCode == character);
            if (character <= group.EndCharCode)
            {
                return group.GlyphId;
            }

            return 0;
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            foreach (var group in Groups)
            {
                var start = group.StartCharCode;
                var stop = group.EndCharCode;
                for (uint u = start; u <= stop; ++u)
                {
                    unicodes.Add(u);
                }
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            throw new NotImplementedException();
        }
    }
}