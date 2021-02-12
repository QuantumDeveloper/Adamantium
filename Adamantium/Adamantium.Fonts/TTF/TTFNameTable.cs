using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFNameTable
    {
        public UInt16 Format { get; set; }
        public UInt16 Count { get; set; }
        public UInt16 StringOffset { get; set; }
        public TTFNameRecord[] NameRecords { get; set; }
    };
}
