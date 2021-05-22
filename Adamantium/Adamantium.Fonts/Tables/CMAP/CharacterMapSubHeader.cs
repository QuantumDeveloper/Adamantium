using System;

namespace Adamantium.Fonts.Tables.CMAP
{
    public struct CharacterMapSubHeader
    {
        public UInt16 firstCode;

        public UInt16 entryCount;

        public Int16 IdDelta;

        public UInt16 IdRangeOffset;
    }
}