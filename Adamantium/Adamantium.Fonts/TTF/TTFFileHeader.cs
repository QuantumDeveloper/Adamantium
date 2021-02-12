using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFFileHeader
    {
        public UInt16 MajorVersion { get; set; } // font major version
        public UInt16 MinorVersion { get; set; } // font minor version
        public UInt16 NumTables { get; set; } // number of table entries
        public UInt16 SearchRange { get; set; } // used for binary searches of the table entries (not necessary)
        public UInt16 EntrySelector { get; set; } // used for binary searches of the table entries (not necessary)
        public UInt16 RangeShift { get; set; } // used for binary searches of the table entries (not necessary)
    };
}