namespace Adamantium.Fonts.Tables.Layout
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
    
    internal class LookupSubtableType1 : LookupSubtable
    {
        private uint format;
        
        public LookupSubtableType1(CoverageTable coverage, ValueRecord record)
        {
            format = 1;
            Coverage = coverage;
            ValueRecords = new[] {record};
        }
        
        public LookupSubtableType1(CoverageTable coverage, ValueRecord[] records)
        {
            format = 2;
            Coverage = coverage;
            ValueRecords = records;
        }
        
        public override uint Type => 1;

        public uint Format => format;
        
        public CoverageTable Coverage { get; }
        
        public ValueRecord[] ValueRecords { get; }
    }
}