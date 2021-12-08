namespace Adamantium.Fonts.Tables.Layout
{
    internal readonly struct MarkRecord
    {
        /// <summary>
        /// Class defined for this mark,. A mark class is identified by a specific integer, called a class value
        /// </summary>
        public readonly ushort MarkClass;

        /// <summary>
        /// Offset to Anchor table from beginning of MarkArray table
        /// </summary>
        public readonly ushort Offset;

        public MarkRecord(ushort markClass, ushort offset)
        {
            MarkClass = markClass;
            Offset = offset;
        }
        
        public override string ToString()
        {
            return $"Class: {MarkClass}, Offset: {Offset}";
        }
    }
}