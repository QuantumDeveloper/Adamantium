namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType7Format2 : LookupSubtable
    {
        public override uint Type => 7;
        
        public CoverageTable Coverage { get; set; }
        
        public ClassDefTable ClassDef { get; set; }
        
        public ClassSequenceRuleSetTable[] ClassSequenceRuleSets { get; set; } 

    }
}