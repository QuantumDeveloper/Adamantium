using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.Layout;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables.GPOS
{
    /// <summary>
    /// MarkMarkPosFormat1
    /// </summary>
    internal class MarkToMarkAttachmentPositioningSubtable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.MarkToMarkAttachment;
        
        public CoverageTable Mark1Coverage { get; set; }
        
        public CoverageTable Mark2Coverage { get; set; }
        
        public MarkArrayTable Mark1ArrayTable { get; set;}
        
        public Mark2ArrayTable Mark2ArrayTable { get; set;}

        public override void PositionGlyph(
            IGlyphPositioning glyphPositioning,
            FeatureInfo featureInfo,
            uint startIndex, 
            uint length)
        {
            var endIndex = Math.Min(startIndex + length, glyphPositioning.Count);

            for (var i = startIndex; i < endIndex; ++i)
            {
                var mark1Index = Mark1Coverage.FindPosition((ushort)glyphPositioning.GetGlyphIndex(i));
                if (mark1Index < 0 ) continue;

                var previousMark = glyphPositioning.FindGlyphBackwardByKind(GlyphClassDefinition.Mark, i, i - 1);
                if (previousMark < 0) continue;

                var mark2Index = Mark2Coverage.FindPosition((ushort) glyphPositioning.GetGlyphIndex((uint)previousMark));
                if (mark2Index < 0) continue;

                var mark1ClassId = Mark1ArrayTable.GetMarkClass(mark1Index);
                var previousAnchor = Mark2ArrayTable.GetAnchorPoint(mark2Index, mark1ClassId);
                var anchor = Mark1ArrayTable.GetAnchorPoint(mark1Index);

                var prevAdvance = glyphPositioning.GetAdvance((uint)previousMark);

                var prevGlyphOffset = glyphPositioning.GetOffset((uint)previousMark);
                var glyphOffset = glyphPositioning.GetOffset(i);
                var xOffset = prevGlyphOffset.X + previousAnchor.XCoordinate -
                              (prevAdvance.X + glyphOffset.X + anchor.XCoordinate);
                var yOffset = prevGlyphOffset.Y + previousAnchor.YCoordinate - (glyphOffset.Y + anchor.YCoordinate);
                glyphPositioning.AppendGlyphOffset(featureInfo, i, new Vector2F(xOffset, yOffset));
            }
        }
    }
}