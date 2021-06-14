using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ReverseChainingContextualSubstitutionFormat1 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.ReverseChainingContextSingle;

        public CoverageTable Coverage { get; set; }
        
        public CoverageTable[] BacktrackCoverages { get; set; }
        
        public CoverageTable[] LookaheadCoverages { get; set; }
        
        public UInt16[] SubstituteGlyphIDs { get; set; }
    }
}