using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public struct CFFIndex
    {
        public List<uint> Offsets; // offsets array, last offset is pointing to next table, not to data within current table!!!
        public ushort Count; // count of real objects in index table, but real offsets count is +1 because last offset points to next table
        public byte OffsetSize; // size of offset array entry (e.g 2 bytes per each offset)
        public byte[] Data; // data from index table
    }
}
