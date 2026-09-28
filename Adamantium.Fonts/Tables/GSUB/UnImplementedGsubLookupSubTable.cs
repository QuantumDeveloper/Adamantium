namespace Adamantium.Fonts.Tables.GSUB
{
    internal class UnImplementedGSUBLookupSubTable : GSUBLookupSubTable
    {
        public override GSUBLookupType Type { get; }
        
        public uint Format { get; }
        
        public string Message { get; private set; }

        public UnImplementedGSUBLookupSubTable(GSUBLookupType type, uint format, string message = "")
        {
            Type = type;
            Format = format;
            Message = message;
        }

        public override string ToString()
        {
            return $"Type = {Type}, Format = {Format}, Message: {Message}";
        }
    }
}