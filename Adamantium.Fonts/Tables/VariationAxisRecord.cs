using System;

namespace Adamantium.Fonts.Tables
{
    internal class VariationAxisRecord
    {
        public string AxisTag { get; set; }
        public double MinValue { get; set; }
        public double DefaultValue { get; set; }
        public double MaxValue { get; set; }
        public UInt16 Flags { get; set; }
        public UInt16 AxisNameID { get; set; }
    }
}