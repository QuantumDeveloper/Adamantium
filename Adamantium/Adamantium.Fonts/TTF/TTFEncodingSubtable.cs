using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFEncodingSubtable
    {
        public UInt16 Format { get; set; }
        public UInt16 Length { get; set; }
        public UInt16 Language { get; set; }
        public UInt16 SegCountX2 { get; set; }
        public UInt16 SearchRange { get; set; }
        public UInt16 EntrySelector { get; set; }
        public UInt16 RangeShift { get; set; }
        public UInt16[] EndCodes { get; set; }
        public UInt16[] StartCodes { get; set; }
        public UInt16[] IdDeltas { get; set; }
        public UInt16[] IdRangeOffsets { get; set; }
    };
}
