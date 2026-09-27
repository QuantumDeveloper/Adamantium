namespace Adamantium.Fonts.Tables.Layout
{
    internal class MarkArrayTable
    {
        public MarkRecord[] MarkRecords { get; set; }
        
        public AnchorPointTable[] AnchorTables { get; set; }

        public AnchorPointTable GetAnchorPoint(int index)
        {
            return AnchorTables[index];
        }

        public ushort GetMarkClass(int index)
        {
            return MarkRecords[index].MarkClass;
        }
    }
}