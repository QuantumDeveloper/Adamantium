namespace Adamantium.Fonts.Tables.GPOS
{
    internal class UnImplementedGposLookupSubTable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type { get; }
        
        public uint Format { get; }
        
        public string Message { get; private set; }

        public UnImplementedGposLookupSubTable(GPOSLookupType type, uint format, string message = "")
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