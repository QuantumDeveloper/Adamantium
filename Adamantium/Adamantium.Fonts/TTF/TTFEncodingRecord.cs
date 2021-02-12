using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFEncodingRecord
    {
        public UInt16 PlatformId { get; set; }
        public UInt16 EncodingId { get; set; }
        public UInt32 SubtableOffset { get; set; }
        public TTFEncodingSubtable Subtable { get; set; }

        public TTFEncodingRecord()
        {
            Subtable = new TTFEncodingSubtable();
        }
    };
}
