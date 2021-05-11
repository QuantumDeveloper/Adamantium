using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables
{
    internal class TableDirectory
    {
        public TableDirectory()
        {
            TablesOffsets = new Dictionary<string, long>();
        }
        
        public UInt32 SfntVersion { get; set; }
        
        public UInt16 NumTables { get; set; }
        
        public OutlineType OutlineType { get; set; }
        
        // "tag-offset" mapping
        public Dictionary<string, long> TablesOffsets { get; }
        
        public Dictionary<string, TableEntry> TablesEntries { get; private set; }
        
        public TableEntry[] Tables { get; set; }

        public void CreateTableEntriesMap()
        {
            TablesEntries = Tables.ToDictionary(x => x.Name);
            foreach (var table in Tables)
            {
                TablesOffsets[table.Name] = table.Offset;
            }
        }
    }
}
