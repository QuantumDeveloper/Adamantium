using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class AlternateSubstitutionSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Alternate;
        
        public CoverageTable Coverage { get; set; }
        
        public AlternateSetTable[] AlternateSetTables { get; set; }

        public override bool SubstituteGlyphs(
            IGlyphSubstitutions substitutions,
            FeatureInfo featureInfo, 
            uint index,
            uint length)
        {
            var position = Coverage.FindPosition((ushort)substitutions.GetGlyphIndex(index));

            if (position > -1)
            {
                var alt = AlternateSetTables[index];
                substitutions.Replace(featureInfo, index, alt.AlternateGlyphIDs);
                return true;
            }
            
            return false;
        }
    }
}