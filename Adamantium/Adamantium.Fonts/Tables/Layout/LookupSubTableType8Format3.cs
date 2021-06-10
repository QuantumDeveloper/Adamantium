namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType8Format3 : LookupSubtable
    {
        public override uint Type => 8;
        
        public CoverageTable[] BacktrackCoverages { get; set; }
        
        public CoverageTable[] InputCoverages { get; set; }
        
        public CoverageTable[] LookaheadCoverages { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; }
    }
}