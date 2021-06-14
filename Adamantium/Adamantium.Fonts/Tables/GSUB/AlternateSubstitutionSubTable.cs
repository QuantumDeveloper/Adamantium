using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class AlternateSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Alternate;
        
        public CoverageTable Coverage { get; set; }
        
        public AlternateSetTable[] AlternateSetTables { get; set; }
    }
}