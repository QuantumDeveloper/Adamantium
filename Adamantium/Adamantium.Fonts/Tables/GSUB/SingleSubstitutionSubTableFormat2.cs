using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class SingleSubstitutionSubTableFormat2 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Single;
        
        public CoverageTable Coverage { get; set; }
        
        public UInt16[] SubstituteGlyphIds { get; set; }
    }
}