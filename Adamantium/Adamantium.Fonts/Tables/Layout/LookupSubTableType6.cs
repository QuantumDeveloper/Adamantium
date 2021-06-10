namespace Adamantium.Fonts.Tables.Layout
{
    /// <summary>
    /// MarkMarkPosFormat1
    /// </summary>
    internal class LookupSubTableType6 : LookupSubtable
    {
        public override uint Type => 6;
        
        public CoverageTable Mark1Coverage { get; set; }
        
        public CoverageTable Mark2Coverage { get; set; }
        
        public MarkArrayTable Mark1ArrayTable { get; set;}
        
        public Mark2ArrayTable Mark2ArrayTable { get; set;}
    }
}