using System;

namespace Adamantium.Fonts.OTF
{
    internal readonly struct FDRange
    {
        public readonly UInt32 First;
        public readonly byte FontDictIndex;

        public FDRange(UInt32 first, byte fd)
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