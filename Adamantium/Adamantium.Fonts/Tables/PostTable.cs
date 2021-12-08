using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables
{
    internal class PostTable
    {
        internal PostTable()
        {
            GlyphNames = new Dictionary<uint, string>();
        }
        
        public uint Version { get; internal set; }
        
        public uint ItalicAngle { get; internal set; }
        
        public uint UnderlinePosition { get; internal set; }
        
        public uint UnderlineThickness { get; internal set; }
        
        public bool IsMonospaced { get; internal set; }
        
        public Dictionary<UInt32, string> GlyphNames { get; }
    }
}