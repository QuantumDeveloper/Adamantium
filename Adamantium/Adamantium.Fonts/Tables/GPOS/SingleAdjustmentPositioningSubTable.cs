using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Tables.GPOS
{
    // Single Adjustment Positioning: Format 1
    // uint16	posFormat	Format identifier: format = 1
    // Offset16	coverageOffset	Offset to Coverage table, from beginning of SinglePos subtable.
    // uint16	valueFormat	Defines the types of data in the ValueRecord.
    // ValueRecord	valueRecord	Defines positioning value(s) — applied to all glyphs in the Coverage table.
    
    // Single Adjustment Positioning: Format 2
    // uint16	posFormat	Format identifier: format = 2
    // Offset16	coverageOffset	Offset to Coverage table, from beginning of SinglePos subtable.
    // uint16	valueFormat	Defines the types of data in the ValueRecords.
    // uint16	valueCount	Number of ValueRecords — must equal glyphCount in the Coverage table.
    // ValueRecord	valueRecords[valueCount]	Array of ValueRecords — positioning values applied to glyphs.
    
    internal class SingleAdjustmentPositioningSubTable : GPOSLookupSubTable
    {
        private uint format;
        
        public SingleAdjustmentPositioningSubTable(CoverageTable coverage, ValueRecord record)
        {
            format = 1;
            Coverage = coverage;
            ValueRecords = new[] {record};
        }
        
        public SingleAdjustmentPositioningSubTable(CoverageTable coverage, ValueRecord[] records)
        {
            format = 2;
            Coverage = coverage;
            ValueRecords = records;
        }
        
        public override GPOSLookupType Type => GPOSLookupType.SingleAdjustment;

        public uint Format => format;
        
        public CoverageTable Coverage { get; }
        
        public ValueRecord[] ValueRecords { get; }

        public override void PositionGlyph(FontLanguage language,
            FeatureInfo feature,
            IGlyphPositioningLookup glyphPositioningLookup,
            uint startIndex,
            uint length)
        {
            var limitIndex = Math.Min(startIndex + length, glyphPositioningLookup.Count);
            for (uint i = startIndex; i < limitIndex; ++i)
            {
                var position = Coverage.FindPosition((ushort) i);
                if (position > -1)
                {
                    var record = ValueRecords[Format == 1 ? 0 : position];
                    glyphPositioningLookup.AppendGlyphOffset(language, feature, i, new Vector2F(record.XPlacement, record.YPlacement));
                    glyphPositioningLookup.AppendGlyphAdvance(language, feature, i, new Vector2F(record.XAdvance, record.YAdvance));
                }
            }
        }
    }
}