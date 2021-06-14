using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class PairAdjustmentPositioningSubTableFormat1 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.PairAdjustment;

        public CoverageTable CoverageTable { get; set; }
        
        public PairSetTable[] PairSetsTables { get; set; }

    }
}