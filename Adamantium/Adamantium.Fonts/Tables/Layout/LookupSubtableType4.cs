namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubtableType4 : LookupSubtable
    {
        public override uint Type => 4;
        
        public CoverageTable Coverage { get; set; }
        
        public CoverageTable BaseCoverageTable { get; set; }
        
        
    }
}