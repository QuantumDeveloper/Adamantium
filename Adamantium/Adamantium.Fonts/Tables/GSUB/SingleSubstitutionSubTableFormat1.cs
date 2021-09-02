using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class SingleSubstitutionSubTableFormat1 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Single;
        
        public CoverageTable Coverage { get; set; }
        
        public Int16 DeltaGlyphId { get; set; }

        public override bool SubstituteGlyphs(FontLanguage language,
            FeatureInfo featureInfo,
            IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
            if (Coverage.FindPosition((ushort)index) > -1)
            {
                substitutionLookup.Replace(index, (uint)(index + DeltaGlyphId));
                return true;
            }

            return false;
        }
    }
}