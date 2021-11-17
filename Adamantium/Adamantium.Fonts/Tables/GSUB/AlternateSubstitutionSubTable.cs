using System;
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
            var limitIndex = Math.Min(index + length, substitutions.Count);
            for (uint i = index; i < limitIndex; ++i)
            {
                var position = Coverage.FindPosition((ushort) substitutions.GetGlyphIndex(i));

                if (position > -1)
                {
                    var alt = AlternateSetTables[i];
                    substitutions.Replace(featureInfo, i, alt.AlternateGlyphIDs);
                }
            }

            //@TODO check the return value and how it is used
            return true;
        }
    }
}