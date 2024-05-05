using System;

namespace Adamantium.Fonts.Tables;

internal class VerticalHeaderTable
{
    public UInt16 MajorVersion { get; set; }
    public UInt16 MinorVersion { get; set; }
    public Int16 Ascender { get; set; }
    public Int16 Descender { get; set; }
    public Int16 LineGap { get; set; }
    public UInt16 AdvanceHeightMax { get; set; }
    public Int16 MinTopSideBearing { get; set; }
    public Int16 MinBottomSideBearing { get; set; }
    public Int16 YMaxExtent { get; set; }
    public Int16 CaretSlopeRise { get; set; }
    public Int16 CaretSlopeRun { get; set; }
    public Int16 CaretOffset { get; set; }
    public Int16 MetricDataFormat { get; set; }
    public UInt16 NumberOfYMetrics { get; set; }
};