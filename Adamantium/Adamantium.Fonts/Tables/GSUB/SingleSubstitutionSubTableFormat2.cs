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

        public override bool SubstituteGlyphs(FontLanguage language, FeatureInfo featureInfo, IGlyphSubstitutionLookup substitutionLookup,
            uint index)
        {
            var foundAt = Coverage.FindPosition((ushort)index);
            if (foundAt > -1)
            {
                substitutionLookup.Replace(index, SubstituteGlyphIds[foundAt]);
                return true;
            }
            
            return false;
        }
    }
}