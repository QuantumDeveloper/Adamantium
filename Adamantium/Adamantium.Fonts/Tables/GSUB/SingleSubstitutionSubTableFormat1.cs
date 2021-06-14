using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class SingleSubstitutionSubTableFormat1 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Single;
        
        public CoverageTable Coverage { get; set; }
        
        public Int16 DeltaGlyphId { get; set; }
    }
}