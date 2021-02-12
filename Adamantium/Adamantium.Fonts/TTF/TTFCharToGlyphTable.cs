using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFCharToGlyphTable
    {
        public UInt16 Version { get; set; }
        public UInt16 NumTables { get; set; }
        public TTFEncodingRecord[] EncodingRecords { get; set; }
    };
}
