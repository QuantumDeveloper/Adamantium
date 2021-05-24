using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubtableType3 : LookupSubtable
    {
        public override uint Type => 3;
        
        public UInt16 Format { get; set; }
        
        public CoverageTable Coverage { get; set; }
        
        public AnchorTable[] EntryAnchors { get; set; }
        
        public AnchorTable[] ExitAnchors { get; set; }
        
    }
}