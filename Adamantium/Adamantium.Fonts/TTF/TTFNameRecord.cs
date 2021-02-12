using System;

namespace Adamantium.Fonts.TTF
{
    internal class TTFNameRecord
    {
        public UInt16 PlatformId { get; set; }
        public UInt16 EncodingId { get; set; }
        public UInt16 LanguageId { get; set; }
        public UInt16 NameId { get; set; }
        public UInt16 Length { get; set; }
        public UInt16 Offset { get; set; }
    };
}
