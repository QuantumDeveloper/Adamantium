using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal struct ClassRangeRecord
    {
        public readonly UInt16 StartGlyphId;

        public readonly UInt16 EndGlyphId;
        
        public readonly UInt16 ClassId;

        public ClassRangeRecord(UInt16 startGlyphId, UInt16 endGlyphId, UInt16 classId)
        {
            StartGlyphId = startGlyphId;
            EndGlyphId = endGlyphId;
            ClassId = classId;
        }
        
        public override string ToString()
        {
            return $"ClassID = {ClassId}: Range [{StartGlyphId}, {EndGlyphId}]";
        }
    }
}