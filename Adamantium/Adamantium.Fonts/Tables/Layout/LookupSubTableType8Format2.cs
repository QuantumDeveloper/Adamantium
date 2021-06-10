namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType8Format2 : LookupSubtable
    {
        public override uint Type => 8;
        
        public CoverageTable Coverage { get; set; }
        
        public ClassDefTable BacktrackClassDef { get; set; }
        
        public ClassDefTable InputClassDef { get; set; }
        
        public ClassDefTable LookaheadClassDef { get; set; }
        
        public ChainedSequenceRuleSetTable[] ChainedSeqRuleSets { get; set; }
    }
}