using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class ChainedContextsPositioningSubtableFormat2 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.ChainedContextPositioning;
        
        public CoverageTable Coverage { get; set; }
        
        public ClassDefTable BacktrackClassDef { get; set; }
        
        public ClassDefTable InputClassDef { get; set; }
        
        public ClassDefTable LookaheadClassDef { get; set; }
        
        public ChainedSequenceRuleSetTable[] ChainedSeqRuleSets { get; set; }
    }
}