using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ContextualSubstitutionSubTableFormat1 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Context;
        
        public CoverageTable Coverage { get; set; }
        
        public SequenceRuleSetTable[] SequenceRuleSets { get; set; } 
    }
}