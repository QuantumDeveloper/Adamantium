using System;
using Adamantium.Fonts.Tables.Layout;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class PairAdjustmentPositioningSubTableFormat2 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.PairAdjustment;
        
        public CoverageTable CoverageTable { get; set; }
        
        public ClassDefTable ClassDef1 { get; set; }
        
        public ClassDefTable ClassDef2 { get; set; }
        
        public Class1Record[] Class1Records { get; set; }

        public override void PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint startIndex, uint length)
        {
            var endIndex = Math.Min(startIndex + length, glyphPositioningLookup.Count);
            for (uint i = 0; i < endIndex; ++i)
            {
                var recordIndex = CoverageTable.FindPosition((ushort)i);
                if (recordIndex == -1) continue;

                var class1No = ClassDef1.GetClassValue((ushort)i);
                if (class1No > -1)
                {
                    ushort secondGlyphIndex = (ushort)(i + 1);
                    var class2No = ClassDef2.GetClassValue(secondGlyphIndex);
                    if (class2No > -1)
                    {
                        var class1Rec = Class1Records[class1No];
                        var pair = class1Rec.Class2Records[class2No];

                        var valueRecord1 = pair.Value1;
                        var valueRecord2 = pair.Value2;

                        if (valueRecord1 != null)
                        {
                            glyphPositioningLookup.AppendGlyphOffset(i, new Vector2F(valueRecord1.XPlacement, valueRecord1.YPlacement));
                            glyphPositioningLookup.AppendGlyphAdvance(i, new Vector2F(valueRecord1.XAdvance, valueRecord1.YAdvance));
                        }

                        if (valueRecord2 != null)
                        {
                            glyphPositioningLookup.AppendGlyphOffset(i + 1, new Vector2F(valueRecord2.XPlacement, valueRecord2.YPlacement));
                            glyphPositioningLookup.AppendGlyphAdvance(i + 1, new Vector2F(valueRecord2.XAdvance, valueRecord2.YAdvance));
                        }
                    }
                }
            }
        }
    }
}