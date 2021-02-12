using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFKerningTable
    {
        public UInt16 Version { get; set; }
        public UInt16 NumTables { get; set; }
        public TTFKerningSubtable[] Subtables { get; set; }
    };
}
