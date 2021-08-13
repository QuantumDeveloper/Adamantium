using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables
{
    internal class GlyphDefinitionTable
    {
        public UInt16 MajorVersion { get; set; }
        
        public UInt16 MinorVersion { get; set; }
        
        public ClassDefTable GlyphClassDefTable { get; set; }
        
        public AttachmentListTable AttachList { get; set; }
        
        public LigatureCaretList LigatureCaretList { get; set; } 
        
        public ClassDefTable MarkAttachClassDefTable { get; set; }
        
        public MarkGlyphSetsTable MarkGlyphSetsTable { get; set; }

        public void FillData(TypeFace typeFace)
        {
            FillClassDefinitions(typeFace);
        }

        private void FillClassDefinitions(TypeFace typeFace)
        {
            switch (GlyphClassDefTable.Format)
            {
                case 1:
                    uint glyphIndex = GlyphClassDefTable.StartGlyphId;
                    var classValues = GlyphClassDefTable.ClassValueArray;
                    for (int i = 0; i < classValues.Length; ++i)
                    {
                        if (typeFace.GetGlyphByIndex(glyphIndex, out var glyph))
                        {
                            glyph.ClassDefinition = (GlyphClassDefinition) classValues[i];
                        }
                    }
                    break;
                case 2:
                    var records = GlyphClassDefTable.ClassRangeRecords;
                    for (int i = 0; i < records.Length; ++i)
                    {
                        var record = records[i];
                        for (uint k = record.StartGlyphId; k < record.EndGlyphId; ++k)
                        {
                            if (typeFace.GetGlyphByIndex(k, out var glyph))
                            {
                                glyph.ClassDefinition = (GlyphClassDefinition)record.ClassId;
                            }
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException(
                        $"Format {GlyphClassDefTable.Format} is not supported for class def");
            }
        }
    }
}