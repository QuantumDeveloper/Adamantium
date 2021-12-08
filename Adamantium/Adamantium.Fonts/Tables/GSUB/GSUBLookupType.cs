using System;

namespace Adamantium.Fonts.Tables.GSUB
{
    public enum GSUBLookupType : UInt16
    {
        Single = 1,
        Multiple = 2,
        Alternate = 3,
        Ligature = 4,
        Context = 5,
        ChainingContext = 6,
        ExtensionSubstitution = 7,
        ReverseChainingContextSingle = 8,
        Reserved = 9,
    }
}