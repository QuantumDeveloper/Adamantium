using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class AlternateSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Alternate;
        
        public CoverageTable Coverage { get; set; }
        
        public AlternateSetTable[] AlternateSetTables { get; set; }

        public override bool SubstituteGlyphs(FontLanguage language, FeatureInfo featureInfo, IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
            var position = Coverage.FindPosition((ushort)index);

            if (position > -1)
            {
                var alt = AlternateSetTables[index];
                substitutionLookup.Replace(index, alt.AlternateGlyphIDs);
                return true;
            }
            
            return false;
        }
    }
}