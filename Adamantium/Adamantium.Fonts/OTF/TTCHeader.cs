using System;

namespace Adamantium.Fonts.OTF
{
    public class TTCHeader
    {
        public string Tag { get; set; }
        public UInt16 MajorVersion { get; set; }
        public UInt16 MinorVersion { get; set; }
        public UInt32 NumFonts { get; set; }
        public UInt32[] TableDirectoryOffsets { get; set; } 
    }
}