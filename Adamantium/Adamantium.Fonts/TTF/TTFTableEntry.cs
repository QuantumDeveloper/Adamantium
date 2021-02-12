using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFTableEntry
    {
        public UInt32 Tag { get; set; } // integer tag
        public String Name { get; set; } // readable tag
        public UInt32 CheckSum { get; set; } // check sum
        public UInt32 OffsetPos { get; set; } // offset from beginning of file
        public UInt32 Length { get; set; } // length of the table in bytes
    };
}
