namespace Adamantium.Fonts.OTF
{
    internal readonly struct FDRange3
    {
        public readonly ushort First;
        public readonly byte FontDictIndex;

        public FDRange3(ushort first, byte fd)
        {
            First = first;
            FontDictIndex = fd;
        }

        public override string ToString()
        {
            return $"First glyph: {First}, Font Dict Index: {FontDictIndex}";
        }
    }
}