namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType5 : LookupSubtable
    {
        public override uint Type => 5;
        
        public CoverageTable MarkCoverage { get; set; }
        
        public CoverageTable LigatureCoverage { get; set; }
        
        public MarkArrayTable MarkArrayTable { get; set;}
        
        public LigatureArrayTable LigatureArrayTable { get; set; }
    }
}