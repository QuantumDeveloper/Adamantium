namespace Adamantium.Fonts.Parsers
{
    internal interface IFontParser
    {
        public Typeface Typeface { get; }

        public void Parse();

        public void ReadFontName();

        public byte[] GetFontBytes();

    }
}