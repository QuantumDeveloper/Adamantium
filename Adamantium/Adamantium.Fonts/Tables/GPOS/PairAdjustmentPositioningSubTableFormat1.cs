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
            var endIndex = Math.Min(startIndex + length, glyphPositioning.Count - 1);
            for (uint i = startIndex; i < endIndex; ++i)
            {
                var glyphIndex = glyphPositioning.GetGlyphIndex(i);
                var firstFoundGlyph = CoverageTable.FindPosition((ushort)glyphIndex);
                if (firstFoundGlyph == -1) continue;

                var pairSetTable = PairSetsTables[firstFoundGlyph];

                var secondGlyphIndex = glyphPositioning.GetGlyphIndex(i + 1);

                if (pairSetTable.FindPairSet((ushort) secondGlyphIndex, out var foundPairSet))
                {
                    var valueRecord1 = foundPairSet.ValueRecord1;
                    var valueRecord2 = foundPairSet.ValueRecord2;
                    
                    if (valueRecord1 != null)
                    {
                        glyphPositioning.AppendGlyphOffset(feature, i, new Vector2F(valueRecord1.XPlacement, valueRecord1.YPlacement));
                        glyphPositioning.AppendGlyphAdvance(feature, i, new Vector2F(valueRecord1.XAdvance, valueRecord1.YAdvance));
                    }

                    if (valueRecord2 != null)
                    {
                        glyphPositioning.AppendGlyphOffset(feature, i + 1, new Vector2F(valueRecord2.XPlacement, valueRecord2.YPlacement));
                        glyphPositioning.AppendGlyphAdvance(feature, i + 1, new Vector2F(valueRecord2.XAdvance, valueRecord2.YAdvance));
                    }
                }
            }
        }
    }
}