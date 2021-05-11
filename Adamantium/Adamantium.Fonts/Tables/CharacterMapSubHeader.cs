using System;

namespace Adamantium.Fonts.Tables
{
    public struct CharacterMapSubHeader
    {
        public UInt16 firstCode;

        public UInt16 entryCount;

        public Int16 IdDelta;

        public UInt16 IdRangeOffset;
    }
}