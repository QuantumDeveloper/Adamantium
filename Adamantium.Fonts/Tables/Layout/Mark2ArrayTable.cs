namespace Adamantium.Fonts.Tables.Layout
{
    internal class Mark2ArrayTable
    {
        public Mark2Record[] Records { get; set; }
        
        public AnchorPointTable GetAnchorPoint(int index, int classId)
        {
            return Records[index].Anchors[classId];
        }
        
    }
}