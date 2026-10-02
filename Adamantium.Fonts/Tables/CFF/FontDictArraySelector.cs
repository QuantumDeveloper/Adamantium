using System;

namespace Adamantium.Fonts.Tables.CFF
{
    internal class FontDictArraySelector
    {
        private readonly CIDFontInfo info;

        public FontDictArraySelector(CIDFontInfo info)
        {
            this.info = info;
        }

        public int SelectFontDictArray(UInt32 glyphIndex)
        {
            switch (info.FdSelectFormat)
            {
                case 0:
                    return info.FdRanges0[glyphIndex];
                case 3:
                case 4:
                    return SelectFromRanges(glyphIndex);
                default:
                    throw new NotSupportedException($"Format {info.FdSelectFormat} is not currently supported");
            }
        }

        private int SelectFromRanges(UInt32 glyphIndex)
        {
            var ranges = info.FdRanges;
            var sentinel = ranges.Length - 1;
            if (sentinel < 1 || glyphIndex < ranges[0].First || glyphIndex >= ranges[sentinel].First)
            {
                throw new ArgumentException($"Failed to find correct FD range for Glyph index {glyphIndex}");
            }

            var low = 0;
            var high = sentinel - 1;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (ranges[middle].First <= glyphIndex)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return ranges[low].FontDictIndex;
        }
    }
}
