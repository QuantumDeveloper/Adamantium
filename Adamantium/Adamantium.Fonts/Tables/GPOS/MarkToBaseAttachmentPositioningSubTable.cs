using System;
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

        public override void PositionGlyph(IGlyphPositioningLookup glyphPositioningLookup, uint startIndex, uint length)
        {
            var endIndex = Math.Min(startIndex + length, glyphPositioningLookup.Count);

            for (var i = startIndex; i < endIndex; ++i)
            {
                var markIndex = MarkCoverage.FindPosition((ushort) i);
                if (markIndex < 0) continue;

                int j = glyphPositioningLookup.FindGlyphBackwardByKind(GlyphClassDefinition.Base, i, i - 1);
                if (j < 0)
                {
                    j = glyphPositioningLookup.FindGlyphBackwardByKind(GlyphClassDefinition.Zero, i, i - 1);
                    if (j < 0) continue;
                }

                var baseIndex = BaseCoverage.FindPosition((ushort) j);
                if (baseIndex < 0) continue;

                var baseRecord = BaseArrayTable.BaseRecords[baseIndex];
                var markClass = MarkArrayTable.GetMarkClass(markIndex);

                var anchor = MarkArrayTable.GetAnchorPoint(markClass);
                var previousAnchor = baseRecord.Anchors[markClass];
                var previousOffset = glyphPositioningLookup.GetOffset((uint) j);
                var previousAdvance = glyphPositioningLookup.GetAdvance((uint) j);
                var currentOffset = glyphPositioningLookup.GetOffset(i);
                var xOffset = previousOffset.X + previousAnchor.XCoordinate -
                              (previousAdvance.X + currentOffset.X + anchor.XCoordinate);
                var yOffset = previousOffset.Y + previousAnchor.YCoordinate - (currentOffset.Y + anchor.YCoordinate);
                glyphPositioningLookup.AppendGlyphOffset(i, new Vector2F(xOffset, yOffset));
            }

        }
    }
}