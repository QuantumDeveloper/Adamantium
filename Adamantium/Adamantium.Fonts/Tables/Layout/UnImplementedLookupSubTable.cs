namespace Adamantium.Fonts.Tables.Layout
{
    internal class UnImplementedLookupSubTable : LookupSubtable
    {
        public override uint Type { get; }

        public UnImplementedLookupSubTable(uint type)
        {
            Type = type;
        }
    }
}