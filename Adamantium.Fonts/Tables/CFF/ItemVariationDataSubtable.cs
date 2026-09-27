using System;

namespace Adamantium.Fonts.Tables.CFF
{
    internal class ItemVariationDataSubtable
    {
        public UInt16 ItemCount { get; set; }
        
        public UInt16 ShortDeltaCount { get; set; }
        
        public UInt16 RegionIndexCount { get; set; }
        
        public UInt16[] RegionIndices { get; set; }
        
        public DeltaSet[] DeltaSets { get; set; }
    }
}