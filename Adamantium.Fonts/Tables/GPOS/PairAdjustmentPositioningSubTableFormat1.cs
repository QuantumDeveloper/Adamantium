using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class PairAdjustmentPositioningSubTableFormat1 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.PairAdjustment;

        public CoverageTable CoverageTable { get; set; }

        public PairSetTable[] PairSetsTables { get; set; }

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

            var firstFoundGlyph = CoverageTable.FindPosition((ushort)glyphPositioning.GetGlyphIndex(index));
            if (firstFoundGlyph == -1)
            {
                return false;
            }

            var secondGlyphIndex = glyphPositioning.GetGlyphIndex(index + 1);
            if (!PairSetsTables[firstFoundGlyph].FindPairSet((ushort)secondGlyphIndex, out var foundPairSet))
            {
                return false;
            }

            var valueRecord1 = foundPairSet.ValueRecord1;
            var valueRecord2 = foundPairSet.ValueRecord2;

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
