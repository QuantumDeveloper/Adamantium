using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.Layout;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class MarkToBaseAttachmentPositioningSubTable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.MarkToBaseAttachment;

        public CoverageTable MarkCoverage { get; set; }

        public CoverageTable BaseCoverage { get; set; }

        public BaseArrayTable BaseArrayTable { get; set; }

        public MarkArrayTable MarkArrayTable { get; set; }

        public override void PositionGlyph(
            IGlyphPositioning glyphPositioning,
            FeatureInfo feature,
            uint startIndex, 
            uint length)
        {
            var endIndex = Math.Min(startIndex + length, glyphPositioning.Count);

            for (var i = startIndex; i < endIndex; ++i)
            {
                var markIndex = MarkCoverage.FindPosition((ushort)glyphPositioning.GetGlyphIndex(i));
                if (markIndex < 0) continue;

                int j = glyphPositioning.FindGlyphBackwardByKind(GlyphClassDefinition.Base, i, i - 1);
                if (j < 0)
                {
                    j = glyphPositioning.FindGlyphBackwardByKind(GlyphClassDefinition.Zero, i, i - 1);
                    if (j < 0) continue;
                }

                var prevGlyphIndex = glyphPositioning.GetGlyphIndex((uint)j);
                var baseIndex = BaseCoverage.FindPosition((ushort)prevGlyphIndex);
                if (baseIndex < 0) continue;

                var baseRecord = BaseArrayTable.BaseRecords[baseIndex];
                var markClass = MarkArrayTable.GetMarkClass(markIndex);

                var anchor = MarkArrayTable.GetAnchorPoint(markClass);
                var previousAnchor = baseRecord.Anchors[markClass];
                var previousOffset = glyphPositioning.GetOffset((uint) j);
                var previousAdvance = glyphPositioning.GetAdvance((uint) j);
                var currentOffset = glyphPositioning.GetOffset(i);
                var xOffset = previousOffset.X + previousAnchor.XCoordinate -
                              (previousAdvance.X + currentOffset.X + anchor.XCoordinate);
                var yOffset = previousOffset.Y + previousAnchor.YCoordinate - (currentOffset.Y + anchor.YCoordinate);
                glyphPositioning.AppendGlyphOffset(feature, i, new Vector2F(xOffset, yOffset));
            }

        }
    }
}