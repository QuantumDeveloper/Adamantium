using System;

namespace Adamantium.Fonts.OTF
{
    internal class FontDictArraySelector
    {
        private CIDFontInfo info;
        private FDRange currentRange;
        private int currentRangeIndex = 0;
        private uint endGlyphIndex;
        
        public FontDictArraySelector(CIDFontInfo info)
        {
            this.info = info;
            if (info.FdSelectFormat == 3)
            {
                currentRange = info.FdRanges[0];
                endGlyphIndex = info.FdRanges[1].First;
            }
        }

        public int SelectFontDictArray(UInt32 glyphIndex)
        {
            switch (info.FdSelectFormat)
            {
                case 0:
                    return SelectFontDictRange0(glyphIndex);
                case 3:
                    return SelectFontDictRange3(glyphIndex);
                default:
                    throw new NotSupportedException($"Format {info.FdSelectFormat} is not currently supported");
            }
        }

        private int SelectFontDictRange0(UInt32 glyphId)
        {
            return info.FdRanges0[glyphId];
        }
        
        private int SelectFontDictRange3(UInt32 glyphIdx)
        {
            if (IsGlyphIndexInCurrentRange(glyphIdx))
            {
                return info.FdRanges[currentRangeIndex].FontDictIndex;
            }

            currentRangeIndex++;
            currentRange = info.FdRanges[currentRangeIndex];
            endGlyphIndex = info.FdRanges[currentRangeIndex + 1].First;
            if (IsGlyphIndexInCurrentRange(glyphIdx))
            {
                return info.FdRanges[currentRangeIndex].FontDictIndex;
            }
            else
            {
                throw new ArgumentException($"Failed to find correct FD range for Glyph index {glyphIdx}");
            }
        }

        private bool IsGlyphIndexInCurrentRange(UInt32 glyphIdx)
        {
            return glyphIdx >= currentRange.First && glyphIdx < endGlyphIndex;
        }
    }
}