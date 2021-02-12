using System;

namespace Adamantium.Fonts.OTF
{
    public struct OffsetTable
    {
        public UInt32 SfntVersion;
        public UInt16 numTables;
        public OutlinesType outlinesType;
    }
}
