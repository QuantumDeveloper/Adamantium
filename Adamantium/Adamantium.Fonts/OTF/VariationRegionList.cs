using System;

namespace Adamantium.Fonts.OTF
{
    internal class VariationRegionList
    {
        public UInt16 AxisCount { get; set; }
        
        public UInt16 RegionCount { get; set; }
        
        public VariationRegion[] VariationRegions { get; set; }

        public override string ToString()
        {
            return $"Regions: {RegionCount}, Axis count per region: {AxisCount}";
        }
    }
}