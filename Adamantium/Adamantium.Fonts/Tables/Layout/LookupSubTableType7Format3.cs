namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType7Format3 : LookupSubtable
    {
        public override uint Type => 7;
        
        public CoverageTable[] CoverageTables { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; } 

    }
}