using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ChainedContextsSubstitutionSubTableFormat3 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.ChainingContext;
        
        public CoverageTable[] BacktrackCoverages { get; set; }
        
        public CoverageTable[] InputCoverages { get; set; }
        
        public CoverageTable[] LookaheadCoverages { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; }
    }
}