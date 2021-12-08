using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class LigatureTable
    {
        public UInt16 LigatureGlyphID { get; set; }
        
        public UInt16[] ComponentGlypIDs { get; set; }
    }
}