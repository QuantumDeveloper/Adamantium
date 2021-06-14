using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    /// <summary>
    /// MarkMarkPosFormat1
    /// </summary>
    internal class MarkToMarkAttachmentPositioningSubtable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.MarkToMarkAttachment;
        
        public CoverageTable Mark1Coverage { get; set; }
        
        public CoverageTable Mark2Coverage { get; set; }
        
        public MarkArrayTable Mark1ArrayTable { get; set;}
        
        public Mark2ArrayTable Mark2ArrayTable { get; set;}
    }
}