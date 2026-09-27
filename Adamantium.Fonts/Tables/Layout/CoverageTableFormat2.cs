using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class CoverageTableFormat2 : CoverageTable
    {
        public override uint Format => 2;
        
        public int RangeCount { get; set; }
        
        public UInt16[] StartIndices { get; set; }
        
        public UInt16[] EndIndices { get; set; }
        
        public UInt16[] CoverageIndices { get; set; }
        
        public override int FindPosition(ushort glyphIndex)
        {
            // Ranges must be in glyph ID order, and they must be distinct, with no overlapping.
            // [...] quick calculation of the Coverage Index for any glyph in any range using the
            // formula: Coverage Index (glyphID) = startCoverageIndex + glyphID - startGlyphID.
            // (https://www.microsoft.com/typography/otspec/chapter2.htm#coverageFormat2)
            int n = Array.BinarySearch(EndIndices, glyphIndex);
            n = n < 0 ? ~n : n;
            if (n >= RangeCount || glyphIndex < StartIndices[n])
            {
                return -1;
            }
            return CoverageIndices[n] + glyphIndex - StartIndices[n];
        }

        public override IEnumerable<ushort> GetExpandedValues()
        {
            var indices = new List<ushort>();
            for (int i = 0; i < RangeCount; ++i)
            {
                for (ushort k = StartIndices[i]; k <= EndIndices[i]; ++k)
                {
                    indices.Add(k);
                }
            }

            return indices;
        }
    }
}