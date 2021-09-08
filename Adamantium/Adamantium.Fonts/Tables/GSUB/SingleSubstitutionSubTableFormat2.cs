using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class SingleSubstitutionSubTableFormat2 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Single;
        
        public CoverageTable Coverage { get; set; }
        
        /// <summary>
        /// It provides an array of output glyph indices (Substitute) explicitly matched to the input glyph indices specified in the Coverage table
        /// </summary>
        public UInt16[] SubstituteGlyphIds { get; set; }

        public override bool SubstituteGlyphs(
            IGlyphSubstitutions substitutions,
            FeatureInfo featureInfo, 
            uint index,
            uint length)
        {
            var foundAt = Coverage.FindPosition((ushort)substitutions.GetGlyphIndex(index));
            if (foundAt > -1)
            {
                substitutions.Replace(featureInfo, index, SubstituteGlyphIds[foundAt]);
                return true;
            }
            
            return false;
        }
    }
}