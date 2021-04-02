using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public class TableDirectory
    {
        public UInt32 SfntVersion;
        public UInt16 numTables;
        public OutlineType OutlineType;
        // "tag-offset" mapping
        public Dictionary<string, uint> TablesOffsets;
    }
}
