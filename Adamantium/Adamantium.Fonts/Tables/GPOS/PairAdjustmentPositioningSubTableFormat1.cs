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

        public override void PositionGlyph(FontLanguage language, FeatureInfo feature,
            IGlyphPositioningLookup glyphPositioningLookup,
            uint startIndex, uint length)
        {
            var endIndex = Math.Min(startIndex + length, glyphPositioningLookup.Count);
            for (uint i = 0; i < endIndex; ++i)
            {
                var firstFoundGlyph = CoverageTable.FindPosition((ushort)i);
                if (firstFoundGlyph == -1) continue;

                var pairSet = PairSetsTables[firstFoundGlyph];

                var secondGlyphIndex = i + 1;

                if (pairSet.FindPairSet((ushort) secondGlyphIndex, out var foundPairSet))
                {
                    var valueRecord1 = foundPairSet.ValueRecord1;
                    var valueRecord2 = foundPairSet.ValueRecord2;
                    
                    if (valueRecord1 != null)
                    {
                        glyphPositioningLookup.AppendGlyphOffset(language, feature, i, new Vector2F(valueRecord1.XPlacement, valueRecord1.YPlacement));
                        glyphPositioningLookup.AppendGlyphAdvance(language, feature, i, new Vector2F(valueRecord1.XAdvance, valueRecord1.YAdvance));
                    }

                    if (valueRecord2 != null)
                    {
                        glyphPositioningLookup.AppendGlyphOffset(language, feature, i + 1, new Vector2F(valueRecord2.XPlacement, valueRecord2.YPlacement));
                        glyphPositioningLookup.AppendGlyphAdvance(language, feature, i + 1, new Vector2F(valueRecord2.XAdvance, valueRecord2.YAdvance));
                    }
                }
            }
        }
    }
}