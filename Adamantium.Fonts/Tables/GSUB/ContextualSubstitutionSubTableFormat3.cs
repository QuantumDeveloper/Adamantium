using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class ContextualSubstitutionSubTableFormat3 : GSUBLookupSubTable
    {
        public override GSUBLookupType Type => GSUBLookupType.Context;
        
        public UInt16 GlyphCount { get; set; }
        
        public CoverageTable[] CoverageTables { get; set; }
        
        public SequenceLookupRecord[] LookupRecords { get; set; }
    }
}