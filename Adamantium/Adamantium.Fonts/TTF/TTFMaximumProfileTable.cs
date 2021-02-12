using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFMaximumProfileTable
    {
        public UInt16 MajorVersion { get; set; }
        public UInt16 MinorVersion { get; set; }
        public UInt16 NumGlyphs { get; set; }
        public UInt16 MaxPoints { get; set; }
        public UInt16 MaxContours { get; set; }
        public UInt16 MaxCompositePoints { get; set; }
        public UInt16 MaxCompositeContours { get; set; }
        public UInt16 MaxZones { get; set; }
        public UInt16 MaxTwilightPoints { get; set; }
        public UInt16 MaxStorage { get; set; }
        public UInt16 MaxFunctionDefs { get; set; }
        public UInt16 MaxInstructionDefs { get; set; }
        public UInt16 MaxStackElements { get; set; }
        public UInt16 MaxSizeOfInstructions { get; set; }
        public UInt16 MaxComponentElements { get; set; }
        public UInt16 MaxComponentDepth { get; set; }
    };
}
