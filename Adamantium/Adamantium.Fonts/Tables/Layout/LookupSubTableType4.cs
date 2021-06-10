namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType4 : LookupSubtable
    {
        public override uint Type => 4;
        
        public CoverageTable MarkCoverage { get; set; }
        
        public CoverageTable BaseCoverage { get; set; }
        
        public BaseArrayTable BaseArrayTable { get; set; }
        
        public MarkArrayTable MarkArrayTable { get; set;}
        
        
    }
}