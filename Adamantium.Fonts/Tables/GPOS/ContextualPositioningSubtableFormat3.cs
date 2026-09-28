using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class ContextualPositioningSubtableFormat3 : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.ContextPositioning;
        
        public UInt16 GlyphCount { get; set; }
        
        public CoverageTable[] CoverageTables { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; } 

    }
}