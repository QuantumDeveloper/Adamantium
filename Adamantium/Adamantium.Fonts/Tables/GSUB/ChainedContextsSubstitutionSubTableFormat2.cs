using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ChainedContextsSubstitutionSubTableFormat2 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.ChainingContext;
        
        public CoverageTable Coverage { get; set; }
        
        public ClassDefTable BacktrackClassDef { get; set; }
        
        public ClassDefTable InputClassDef { get; set; }
        
        public ClassDefTable LookaheadClassDef { get; set; }
        
        public ChainedSequenceRuleSetTable[] ChainedSeqRuleSets { get; set; }
    }
}