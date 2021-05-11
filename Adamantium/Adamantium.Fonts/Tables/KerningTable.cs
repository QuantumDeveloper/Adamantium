using System;

namespace Adamantium.Fonts.Tables
{
    internal class KerningTable
    {
        public UInt16 Version { get; set; }
        public UInt16 NumTables { get; set; }
        public KerningSubtable[] Subtables { get; set; }
    };
}
