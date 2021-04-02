using System;

namespace Adamantium.Fonts.OTF
{
    public struct VariationSelectorRecord
    {
        // Uint24
        public UInt32 VarSelector;

        public UInt32 DefaultUVSOffset;

        public UInt32 NonDefaultUVSOffset;
    }
}