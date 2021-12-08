using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CMAP
{
    public class CharacterMapFormat4 : CharacterMap
    {
        public override UInt16 Format => 4;

        public UInt16 Length { get; set; }

        public UInt16 Language { get; set; }
        
        public UInt16 SegCountX2 { get; set; }
        
        public UInt16 SegCount { get; set; }
        
        public UInt16 SearchRange { get; set; }
        
        public UInt16 EntrySelector { get; set; }
        
        public UInt16 RangeShift { get; set; }
        
        // array size is number of segments count
        public UInt16[] EndCode { get; set; }
        
        public UInt16 ReservedPad { get; set; }
        
        // array size is number of segments count
        public UInt16[] StartCode { get; set; }
        
        // array size is number of segments count
        public Int16[] IdDelta { get; set; }
        
        // array size is number of segments count
        public UInt16[] IdRangeOffsets { get; set; }
        
        public UInt16[] GlyphIdArray { get; set; }
        
        public override uint GetGlyphIndex(uint character)
        {
            if (character > ushort.MaxValue) return 0;

            int i = Array.BinarySearch(EndCode, (ushort)character);
            i = i < 0 ? ~i : i;
            if (i >= EndCode.Length || StartCode[i] > character)
            {
                return 0;
            }

            if (IdRangeOffsets[i] == 0)
            {
                return (ushort)((character + IdDelta[i]) % 65536);
            }

            var offset = (IdRangeOffsets[i] / 2) + (character - StartCode[i]);

            return GlyphIdArray[offset - IdRangeOffsets.Length + i];
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            for (int i = 0; i < StartCode.Length; ++i)
            {
                var start = StartCode[i];
                var stop = EndCode[i];
                for (var j = start; j <= stop; ++j)
                {
                    unicodes.Add(j);
                }
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            for (int i = 0; i < StartCode.Length; ++i)
            {
                ushort start = StartCode[i];
                ushort stop = EndCode[i];
                for (uint j = start; j <= stop; ++j)
                {
                    var glyphIndex = GetGlyphIndex(j);
                    unicodeToGlyph[j] = glyphIndex;
                }
            }
        }
    }
}