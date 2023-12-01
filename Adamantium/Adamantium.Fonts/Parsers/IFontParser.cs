namespace Adamantium.Fonts.Parsers
{
    internal interface IFontParser
    {
        public TypeFace TypeFace { get; }

        public void Parse();

        public byte[] GetFontBytes();

    }
}