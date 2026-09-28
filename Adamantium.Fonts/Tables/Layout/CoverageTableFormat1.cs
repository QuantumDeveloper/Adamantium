using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class CoverageTableFormat1 : CoverageTable
    {
        public override uint Format => 1;
        
        public UInt16[] GlyphIdArray { get; set; }
        
        public override int FindPosition(ushort glyphIndex)
        {
            int n = Array.BinarySearch(GlyphIdArray, glyphIndex);
            return n < 0 ? -1 : n;
        }

        public override IEnumerable<ushort> GetExpandedValues()
        {
            return GlyphIdArray;
        }

        
    }
}