using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class LigatureSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Ligature;
        
        public CoverageTable Coverage { get; set; }
        
        public LigatureSetTable[] LigatureSetTables { get; set; }
    }
}