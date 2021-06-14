using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class MultipleSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Multiple;
        
        public CoverageTable Coverage { get; set; }
        
        public SequenceTable[] SequenceTables { get; set; }
    }
    
    
}