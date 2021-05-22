namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubtableType2Format1 : LookupSubtable
    {
        public override uint Type => 2;

        public CoverageTable CoverageTable { get; set; }
        
        public PairSetTable[] PairSetsTables { get; set; }

    }
    
    internal class LookupSubtableType2Format2 : LookupSubtable
    {
        public override uint Type => 2;
        
        public CoverageTable CoverageTable { get; set; }
        
        public ClassDefTable ClassDef1 { get; set; }
        
        public ClassDefTable ClassDef2 { get; set; }
        
        public Class1Record[] Class1Records { get; set; }
    }
}