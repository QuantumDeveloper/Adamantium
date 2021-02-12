using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFHorizontalHeaderTable
    {
        public UInt16 MajorVersion { get; set; }
        public UInt16 MinorVersion { get; set; }
        public Int16 Ascender { get; set; }
        public Int16 Descender { get; set; }
        public Int16 LineGap { get; set; }
        public UInt16 AdvanceWidthMax { get; set; }
        public Int16 MinLeftSideBearing { get; set; }
        public Int16 MinRightSideBearing { get; set; }
        public Int16 XMaxExtent { get; set; }
        public Int16 CaretSlopeRise { get; set; }
        public Int16 CaretSlopeRun { get; set; }
        public Int16 CaretOffset { get; set; }
        public Int16 MetricDataFormat { get; set; }
        public UInt16 NumberOfHMetrics { get; set; }
    };
}
