using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class MarkToLigatureAttachmentPositioningSubTable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.MarkToLigatureAttachment;
        
        public CoverageTable MarkCoverage { get; set; }
        
        public CoverageTable LigatureCoverage { get; set; }
        
        public MarkArrayTable MarkArrayTable { get; set;}
        
        public LigatureArrayTable LigatureArrayTable { get; set; }
    }
}