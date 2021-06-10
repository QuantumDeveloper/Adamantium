namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType7Format1 : LookupSubtable
    {
        public override uint Type => 7;
        
        public CoverageTable Coverage { get; set; }
        
        public SequenceRuleSetTable[] SequenceRuleSets { get; set; } 

    }
}