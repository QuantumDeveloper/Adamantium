using System;

namespace Adamantium.Fonts.OTF
{
    public struct SubHeader
    {
        public UInt16 firstCode;

        public UInt16 entryCount;

        public Int16 IdDelta;

        public UInt16 IdRangeOffset;
    }
}