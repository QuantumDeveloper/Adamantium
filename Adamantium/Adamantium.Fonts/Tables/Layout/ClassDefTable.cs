using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class ClassDefTable
    {
        public UInt16 Format { get; set; }
        
        public UInt16 StartGlyphId { get; set; }
        
        public UInt16[] ClassValueArray { get; set; }
        
        public ClassRangeRecord[] ClassRangeRecords { get; set; }

        public int GetClassValue(ushort glyphIndex)
        {
            switch (Format)
            {
                case 1:
                    if (glyphIndex >= StartGlyphId &&
                        glyphIndex < ClassValueArray.Length)
                    {
                        return ClassValueArray[glyphIndex - StartGlyphId];
                    }
                    return -1;
                case 2:
                    foreach (var rangeRecord in ClassRangeRecords)
                    {
                        if (rangeRecord.StartGlyphId <= glyphIndex)
                        {
                            if (glyphIndex <= rangeRecord.EndGlyphId)
                            {
                                return rangeRecord.ClassId;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    return -1;
                default:
                    throw new NotSupportedException($"Format {Format} is not supported for ClassDef Table");
            }
        }
    }
}