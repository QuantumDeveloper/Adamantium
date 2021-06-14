using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class CursiveAttachmentPositioningSubTable : GPOSLookupSubTable
    {
        public override GPOSLookupType Type => GPOSLookupType.CursiveAttachment;
        
        public CoverageTable Coverage { get; set; }
        
        public AnchorTable[] EntryAnchors { get; set; }
        
        public AnchorTable[] ExitAnchors { get; set; }
        
    }
}