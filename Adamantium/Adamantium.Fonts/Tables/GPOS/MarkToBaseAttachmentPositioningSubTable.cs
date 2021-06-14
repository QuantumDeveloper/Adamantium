using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class MarkToBaseAttachmentPositioningSubTable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.MarkToBaseAttachment;
        
        public CoverageTable MarkCoverage { get; set; }
        
        public CoverageTable BaseCoverage { get; set; }
        
        public BaseArrayTable BaseArrayTable { get; set; }
        
        public MarkArrayTable MarkArrayTable { get; set;}
        
        
    }
}