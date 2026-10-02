using System;
using Adamantium.Fonts.Common;
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

        public override void PositionGlyph(
            IGlyphPositioning glyphPositioning,
            FeatureInfo feature,
            uint startIndex,
            uint length)
        {
            var endIndex = Math.Min(startIndex + length, glyphPositioning.Count);
            for (uint i = startIndex; i < endIndex; ++i)
            {
                PositionGlyphAt(glyphPositioning, feature, i);
            }
        }

        internal override bool PositionGlyphAt(IGlyphPositioning glyphPositioning, FeatureInfo feature, uint index)
        {
            if (index + 1 >= glyphPositioning.Count)
            {
                return false;
            }

            var glyphIndex = (ushort)glyphPositioning.GetGlyphIndex(index);
            if (CoverageTable.FindPosition(glyphIndex) == -1)
            {
                return false;
            }

            var class1No = ClassDef1.GetClassValue(glyphIndex);
            var class2No = ClassDef2.GetClassValue((ushort)glyphPositioning.GetGlyphIndex(index + 1));
            if (class1No >= Class1Records.Length || class2No >= Class1Records[class1No].Class2Records.Length)
            {
                return false;
            }

            var pair = Class1Records[class1No].Class2Records[class2No];
            var valueRecord1 = pair.Value1;
            var valueRecord2 = pair.Value2;

            if (valueRecord1 != null)
            {
                glyphPositioning.AppendGlyphOffset(feature, index, new Vector2F(valueRecord1.XPlacement, valueRecord1.YPlacement));
                glyphPositioning.AppendGlyphAdvance(feature, index, new Vector2F(valueRecord1.XAdvance, valueRecord1.YAdvance));
            }

            if (valueRecord2 != null)
            {
                glyphPositioning.AppendGlyphOffset(feature, index + 1, new Vector2F(valueRecord2.XPlacement, valueRecord2.YPlacement));
                glyphPositioning.AppendGlyphAdvance(feature, index + 1, new Vector2F(valueRecord2.XAdvance, valueRecord2.YAdvance));
            }

            return true;
        }
    }
}
