using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ChainedContextsSubstitutionSubTableFormat1 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.ChainingContext;
        
        public CoverageTable Coverage { get; set; }
        
        public ChainedSequenceRuleSetTable[] ChainedSeqRuleSets { get; set; }
    }
}