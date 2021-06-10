using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType8Format1 : LookupSubtable
    {
        public override uint Type => 8;
        
        public CoverageTable Coverage { get; set; }
        
        public ChainedSequenceRuleSetTable[] ChainedSeqRuleSets { get; set; }
    }
}