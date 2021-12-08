using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class ChainedContextsPositioningSubtableFormat1 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.ChainedContextPositioning;
        
        public CoverageTable Coverage { get; set; }
        
        public ChainedSequenceRuleSetTable[] ChainedSeqRuleSets { get; set; }
    }
}