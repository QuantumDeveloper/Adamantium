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

        public override bool SubstituteGlyphs(
            IGlyphSubstitutions substitutions,
            FeatureInfo featureInfo,
            uint index,
            uint length)
        {
            var glyphIndex = substitutions.GetGlyphIndex(index);
            if (Coverage.FindPosition((ushort)glyphIndex) > -1)
            {
                substitutions.Replace(featureInfo, index, (uint)(glyphIndex + DeltaGlyphId));
                return true;
            }

            return false;
        }
    }
}