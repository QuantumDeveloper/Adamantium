using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupSubTableType3 : LookupSubtable
    {
        public override uint Type => 3;
        
        public CoverageTable Coverage { get; set; }
        
        public AnchorTable[] EntryAnchors { get; set; }
        
        public AnchorTable[] ExitAnchors { get; set; }
        
    }
}