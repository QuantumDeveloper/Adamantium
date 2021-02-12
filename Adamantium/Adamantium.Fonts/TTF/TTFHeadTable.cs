using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFHeadTable
    {
        public UInt16 MajorVersion { get; set; }
        public UInt16 MinorVersion { get; set; }
        public UInt16 FontRevisionMaj { get; set; }
        public UInt16 FontRevisionMin { get; set; }
        public UInt32 CheckSumAdjustment { get; set; }
        public UInt32 MagicNumber { get; set; } // 0x5F0F3CF5
        public UInt16 Flags { get; set; } // bits
        public UInt16 UnitsPerEm { get; set; }
        public Int64 CreatedDate { get; set; }
        public Int64 ModifiedDate { get; set; }
        public Int16 XMin { get; set; }
        public Int16 YMin { get; set; }
        public Int16 XMax { get; set; }
        public Int16 YMax { get; set; }
        public UInt16 MacStyle { get; set; } // bits: 0 - bold, 1 - italic, etc...
        public UInt16 LowestRecPPEM { get; set; } // smallest readable size in pixels
        public Int16 FontDirectionHint { get; set; }
        public Int16 IndexToLocFormat { get; set; }
        public Int16 GlyphDataFormat { get; set; }
    };
}
