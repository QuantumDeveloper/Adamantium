using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class ChainedContextsPositioningSubtableFormat3 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.ChainedContextPositioning;
        
        public CoverageTable[] BacktrackCoverages { get; set; }
        
        public CoverageTable[] InputCoverages { get; set; }
        
        public CoverageTable[] LookaheadCoverages { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; }
    }
}