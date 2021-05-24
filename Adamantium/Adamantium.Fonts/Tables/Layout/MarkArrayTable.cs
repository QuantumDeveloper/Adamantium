namespace Adamantium.Fonts.Tables.Layout
{
    internal class MarkArrayTable
    {
        public MarkRecord[] MarkRecords { get; set; }
        
        public AnchorTable[] AnchorTables { get; set; }

        public AnchorTable GetAnchorTable(int index)
        {
            return AnchorTables[index];
        }

        public ushort GetMarkClass(int index)
        {
            return MarkRecords[index].MarkClass;
        }
    }
}