using System;

namespace Adamantium.Fonts.Tables.GPOS
{
    public enum GPOSLookupType : UInt16
    {
        SingleAdjustment = 1,
        PairAdjustment = 2,
        CursiveAttachment = 3,
        MarkToBaseAttachment = 4,
        MarkToLigatureAttachment = 5,
        MarkToMarkAttachment = 6,
        ContextPositioning = 7,
        ChainedContextPositioning = 8,
        ExtensionPositioning = 9,
        Reserved
    }
}