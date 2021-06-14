using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ContextualSubstitutionSubTableFormat2 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Context;
        
        public CoverageTable Coverage { get; set; }
        
        public ClassDefTable ClassDef { get; set; }
        
        public ClassSequenceRuleSetTable[] ClassSequenceRuleSets { get; set; }
    }
}