using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class PairAdjustmentPositioningSubTableFormat2 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.PairAdjustment;
        
        public CoverageTable CoverageTable { get; set; }
        
        public ClassDefTable ClassDef1 { get; set; }
        
        public ClassDefTable ClassDef2 { get; set; }
        
        public Class1Record[] Class1Records { get; set; }
    }
}