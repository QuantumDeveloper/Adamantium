using System;

namespace Adamantium.Fonts.OTF
{
    internal class NameTable
    {
        // For version 0 and 1
        public UInt16 Version { get; set; }
        
        public UInt16 Count { get; set; }
        
        public UInt16 StorageOffset { get; set; }
        
        public NameRecord[] NameRecords { get; set; }
        
        // For Version 1
        
        public UInt16 LangTagCount { get; set; }
        
        public LangTagRecord[] LangTagRecords { get; set; }
    }
}